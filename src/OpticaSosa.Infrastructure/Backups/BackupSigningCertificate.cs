using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;

namespace OpticaSosa.Infrastructure.Backups;

public interface IBackupSigningCertificate
{
    // El consumidor dispone su copia; nunca se exporta ni almacena la clave privada con los datos.
    X509Certificate2 Open();
}

public sealed class WindowsBackupSigningCertificate(IOptions<BackupRuntimeOptions> options) : IBackupSigningCertificate
{
    public X509Certificate2 Open()
    {
        var fingerprint = options.Value.SigningCertificateSha256;
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(fingerprint))
            throw new InvalidDataException("No hay certificado de firma autorizado para esta instalación.");
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
        X509Certificate2? selected = null;
        foreach (var certificate in store.Certificates)
        {
            if (selected is null && certificate.HasPrivateKey && string.Equals(certificate.GetCertHashString(HashAlgorithmName.SHA256), fingerprint,
                StringComparison.OrdinalIgnoreCase)) selected = new X509Certificate2(certificate);
            certificate.Dispose();
        }
        if (selected is not null) return selected;
        throw new InvalidDataException("No se encontró la clave de firma en la identidad Windows del proceso. No se generará otra automáticamente.");
    }
}
