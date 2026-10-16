using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using OpticaSosa.Application.Business;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Business;
using OpticaSosa.Infrastructure.Identity;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Business;

public sealed class BusinessSettingsService(IDbContextFactory<OpticaDbContext> factory, ICurrentUser current,
    ILogger<BusinessSettingsService> logger) : IBusinessSettingsService
{
    private BusinessBranding? branding;
    public async Task<BusinessBranding> GetBrandingAsync(CancellationToken ct = default)
    {
        if (branding is not null) return branding;
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var row = await db.BusinessSettings.AsNoTracking().Select(x => new { x.Profile.TradeName, x.LogoVersion }).SingleOrDefaultAsync(ct);
            return branding = row is null ? BusinessBranding.Default
                : new(row.TradeName ?? BusinessBranding.DefaultName, row.LogoVersion);
        }
        catch (SqliteException ex)
        {
            logger.LogWarning("No se pudo leer la marca del negocio. Código SQLite {ErrorCode}.", ex.SqliteErrorCode);
            return branding = BusinessBranding.Default;
        }
    }

    public async Task<BusinessDetails> GetForAdministrationAsync(CancellationToken ct = default)
    {
        Demand();
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.BusinessSettings.AsNoTracking().SingleOrDefaultAsync(ct);
        return row is null ? new(new(null, null, null, null, null, null, null, null, null), Guid.Empty, null)
            : new(row.Profile, row.Version, row.LogoVersion);
    }

    public async Task<BusinessSaveResult> SaveAsync(BusinessProfile profile, Guid version, LogoUpload? logo, bool removeLogo, CancellationToken ct = default)
    {
        Demand();
        if (removeLogo && logo is not null) return new(false, "Elegí reemplazar el logo o quitarlo, no ambas opciones a la vez.");
        byte[]? cleanLogo;
        try
        {
            profile = profile.ValidateAndNormalize();
            cleanLogo = logo is null ? null : LogoProcessor.Normalize(logo);
        }
        catch (ArgumentException ex) { return new(false, ex.Message); }
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.BusinessSettings.SingleOrDefaultAsync(ct);
        if ((row?.Version ?? Guid.Empty) != version) return new(false, "La configuración cambió. Recargá la página antes de guardar.");
        if (row is null) { row = new(); db.BusinessSettings.Add(row); }
        row.Update(profile, cleanLogo, removeLogo);
        db.AuditEntries.Add(new AuditEntry { Action = "business.updated", ActorId = current.Id, TargetId = "business" });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        { return new(false, "No se pudo guardar por un conflicto de datos. Recargá la página e intentá nuevamente."); }
        branding = null;
        return new(true);
    }

    public async Task<byte[]?> GetLogoAsync(Guid version, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.BusinessSettings.AsNoTracking().Where(x => x.LogoVersion == version).Select(x => x.LogoPng).SingleOrDefaultAsync(ct);
    }

    private void Demand()
    {
        if (current.Id is null || !current.HasPermission(Permissions.ManageBusiness)) throw new UnauthorizedAccessException();
    }
}
