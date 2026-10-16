using OpticaSosa.Domain.Business;

namespace OpticaSosa.Application.Business;

public sealed record BusinessBranding(string DisplayName, Guid? LogoVersion)
{
    public const string DefaultName = "Sistema de Gestión para Ópticas";
    public static BusinessBranding Default { get; } = new(DefaultName, null);
}
public sealed record BusinessDetails(BusinessProfile Profile, Guid Version, Guid? LogoVersion);
public sealed record LogoUpload(byte[] Bytes, string FileName, string ContentType)
{
    public const int MaximumBytes = 2 * 1024 * 1024;
}
public sealed record BusinessSaveResult(bool Succeeded, string? Error = null);

public interface IBusinessSettingsService
{
    Task<BusinessBranding> GetBrandingAsync(CancellationToken ct = default);
    Task<BusinessDetails> GetForAdministrationAsync(CancellationToken ct = default);
    Task<BusinessSaveResult> SaveAsync(BusinessProfile profile, Guid version, LogoUpload? logo, bool removeLogo, CancellationToken ct = default);
    Task<byte[]?> GetLogoAsync(Guid version, CancellationToken ct = default);
}
