using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;

namespace OpticaSosa.Infrastructure.Backups;

public sealed class BackupAuthenticity(IOptions<BackupRuntimeOptions> options, IBackupSigningCertificate certificates, TimeProvider clock)
{
    public const string SignatureFile = "manifest.p7s";
    private const string Sha256Oid = "2.16.840.1.101.3.4.2.1";
    private const int MaxSignatureBytes = 65536;
    public bool SigningEnabled => options.Value.SigningEnabled;

    public void CheckCreationPolicy()
    {
        if (options.Value.RequireSignature && !SigningEnabled)
            throw new InvalidDataException("Se exige firma, pero la creación de firmas no está habilitada. No se creará una copia sin firma.");
    }

    public async Task SignAsync(string directory, CancellationToken ct)
    {
        if (!SigningEnabled) return;
        try
        {
            using var certificate = certificates.Open();
            DemandTrusted(certificate);
            var now = clock.GetUtcNow().UtcDateTime;
            if (!certificate.HasPrivateKey || now < certificate.NotBefore.ToUniversalTime() || now > certificate.NotAfter.ToUniversalTime())
                throw new InvalidDataException("El certificado no permite firmar en la fecha actual.");
            var content = await File.ReadAllBytesAsync(BackupFileSafety.Child(directory, "complete.json"), ct);
            var cms = new SignedCms(new ContentInfo(content), detached: true);
            var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificate)
            { DigestAlgorithm = new Oid(Sha256Oid), IncludeOption = X509IncludeOption.EndCertOnly };
            signer.SignedAttributes.Add(new Pkcs9SigningTime(now));
            cms.ComputeSignature(signer, silent: true);
            var bytes = cms.Encode();
            if (bytes.Length > MaxSignatureBytes) throw new InvalidDataException("La firma supera el tamaño permitido.");
            await using var output = new FileStream(BackupFileSafety.Child(directory, SignatureFile), FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 81920, true);
            await output.WriteAsync(bytes, ct); await output.FlushAsync(ct);
        }
        catch (CryptographicException ex) { throw new InvalidDataException("No se pudo firmar el respaldo con la clave autorizada.", ex); }
    }

    public async Task VerifyAsync(string directory, byte[] manifest, int format, CancellationToken ct)
    {
        var signature = BackupFileSafety.Child(directory, SignatureFile);
        if (!File.Exists(signature))
        {
            if (format == 3 || options.Value.RequireSignature) throw new InvalidDataException("Falta la firma exigida para este respaldo.");
            return; // Compatibilidad explícita: formato 2 sin firma no prueba autenticidad.
        }
        await using var input = new FileStream(signature, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        if (input.Length is <= 0 or > MaxSignatureBytes) throw new InvalidDataException("Firma inválida o demasiado grande.");
        var bytes = new byte[(int)input.Length];
        await input.ReadExactlyAsync(bytes, ct);
        try
        {
            var cms = new SignedCms(new ContentInfo(manifest), detached: true);
            cms.Decode(bytes);
            if (!cms.Detached || !cms.ContentInfo.Content.AsSpan().SequenceEqual(manifest) || cms.SignerInfos.Count != 1)
                throw new InvalidDataException("La firma no corresponde al manifiesto esperado.");
            var signer = cms.SignerInfos[0];
            if (signer.Certificate is null || signer.DigestAlgorithm.Value != Sha256Oid)
                throw new InvalidDataException("La firma no usa el certificado o algoritmo requerido.");
            DemandTrusted(signer.Certificate);
            // Confianza por huella previamente autorizada, no por el certificado incluido ni una consulta a internet.
            // La caducidad no invalida por sí sola archivos históricos; revocaciones se administran explícitamente.
            cms.CheckSignature(verifySignatureOnly: true);
        }
        catch (CryptographicException ex) { throw new InvalidDataException("La firma del respaldo no es válida.", ex); }
    }

    private void DemandTrusted(X509Certificate2 certificate)
    {
        var fingerprint = certificate.GetCertHashString(HashAlgorithmName.SHA256);
        if (!options.Value.TrustedSigningCertificatesSha256.Contains(fingerprint, StringComparer.OrdinalIgnoreCase) ||
            options.Value.RevokedSigningCertificatesSha256.Contains(fingerprint, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("El firmante no está autorizado o fue revocado. No se confía en certificados obtenidos del propio respaldo.");
        using var rsa = certificate.GetRSAPublicKey();
        var usage = certificate.Extensions.OfType<X509KeyUsageExtension>().SingleOrDefault();
        if (rsa is null || rsa.KeySize < 3072 || usage is null || !usage.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature))
            throw new InvalidDataException("El certificado requiere RSA de al menos 3072 bits y uso de firma digital.");
    }
}
