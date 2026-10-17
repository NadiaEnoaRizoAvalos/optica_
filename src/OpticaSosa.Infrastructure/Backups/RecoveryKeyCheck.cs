using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace OpticaSosa.Infrastructure.Backups;

public static class RecoveryKeyCheck
{
    // Importación efímera: no instala certificados ni persiste claves bajo la cuenta/equipo.
    public static string Verify(byte[] encryptedPkcs12, ReadOnlySpan<char> password, string trustedSha256)
    {
        if (password.IsEmpty || encryptedPkcs12.Length is 0 or > 1024 * 1024)
            throw new InvalidDataException("Se requiere un PFX protegido y su contraseña, con tamaño máximo de 1 MiB.");
        var container = Pkcs12Info.Decode(encryptedPkcs12, out var read);
        if (read != encryptedPkcs12.Length || container.IntegrityMode != Pkcs12IntegrityMode.Password || !container.VerifyMac(password))
            throw new CryptographicException("El PFX requiere integridad autenticada por contraseña válida.");
        foreach (var contents in container.AuthenticatedSafe)
        {
            if (contents.ConfidentialityMode == Pkcs12ConfidentialityMode.Password) continue;
            if (contents.ConfidentialityMode != Pkcs12ConfidentialityMode.None ||
                contents.GetBags().Any(x => x is Pkcs12KeyBag or Pkcs12SafeContentsBag))
                throw new InvalidDataException("No se aceptan claves privadas en claro ni contenedores anidados no verificados.");
        }
        using var certificate = X509CertificateLoader.LoadPkcs12(encryptedPkcs12, password,
            X509KeyStorageFlags.EphemeralKeySet);
        var fingerprint = certificate.GetCertHashString(HashAlgorithmName.SHA256);
        if (!string.Equals(fingerprint, trustedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("La clave recuperada no corresponde a la huella aprobada fuera del respaldo.");
        using var key = certificate.GetRSAPrivateKey();
        using var publicKey = certificate.GetRSAPublicKey();
        if (key is null || publicKey is null || key.KeySize < 3072 ||
            !certificate.Extensions.OfType<X509KeyUsageExtension>().Any(x => x.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature)))
            throw new InvalidDataException("No se recuperó una clave privada RSA autorizada para firma.");
        var challenge = RandomNumberGenerator.GetBytes(32);
        var signature = key.SignData(challenge, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        if (!publicKey.VerifyData(challenge, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            throw new InvalidDataException("La clave recuperada no superó la comprobación de posesión.");
        return fingerprint;
    }
}
