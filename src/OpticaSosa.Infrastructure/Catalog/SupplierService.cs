using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Catalog;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Infrastructure.Identity;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Catalog;

public sealed class SupplierService(IDbContextFactory<OpticaDbContext> factory, ICurrentUser current, TimeProvider clock)
    : ISupplierService
{
    public async Task<SupplierPage> SearchAsync(string? search, bool includeInactive = false, bool laboratoriesOnly = false,
        int page = 1, CancellationToken ct = default)
    {
        Demand(Permissions.ReadSuppliers);
        if (page < 1 || page > int.MaxValue / 50) throw new ArgumentOutOfRangeException(nameof(page));
        var key = CatalogText.Key(CatalogText.Optional(search, 160) ?? "");
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await db.Suppliers.AsNoTracking().Where(x => (includeInactive || x.IsActive) &&
                (!laboratoriesOnly || x.IsLaboratory) && (key == "" || x.NameKey.Contains(key) ||
                (x.TaxKey != null && x.TaxKey.Contains(key.Replace("-", "").Replace(" ", "")))))
            .OrderBy(x => x.NameKey).ThenBy(x => x.Id).Skip((page - 1) * 50).Take(51).ToListAsync(ct);
        return new(rows.Take(50).Select(Summary).ToArray(), page, rows.Count > 50);
    }

    public async Task<SupplierDetails?> GetAsync(Guid id, CancellationToken ct = default)
    {
        Demand(Permissions.ReadSuppliers);
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return row is null ? null : new(Summary(row), row.Phone, row.Email, row.TaxId, row.Services, row.Notes, row.Version);
    }

    public Task<CatalogResult> CreateAsync(SupplierInput input, CancellationToken ct = default) => SaveAsync(null, input, ct);
    public Task<CatalogResult> UpdateAsync(Guid id, SupplierInput input, CancellationToken ct = default) => SaveAsync(id, input, ct);

    private async Task<CatalogResult> SaveAsync(Guid? id, SupplierInput input, CancellationToken ct)
    {
        var actor = Demand(Permissions.ManageCatalog);
        SupplierProfile profile;
        try { profile = input.ToProfile(); }
        catch (ArgumentException ex) { return new(CatalogResultCode.Invalid, Error: ex.Message); }
        var target = id ?? input.SubmissionId;
        if (target == Guid.Empty || (id is null && !input.IsActive))
            return new(CatalogResultCode.Invalid, Error: "Recargá el formulario y registrá un proveedor activo.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == target, ct);
        if (id is null && row is not null)
            return row.CreatedByUserId == actor && row.IsActive && row.Matches(profile)
                ? new(CatalogResultCode.Success, target) : Conflict();
        if (id is not null && row is null) return new(CatalogResultCode.NotFound, Error: "El proveedor no existe.");
        if (row is not null && row.Version != input.Version) return Conflict();
        if (await db.Suppliers.AnyAsync(x => x.Id != target &&
            (x.NameKey == profile.NameKey || (profile.TaxKey != null && x.TaxKey == profile.TaxKey)), ct))
            return new(CatalogResultCode.Duplicate, Error: "Ya existe ese nombre o identificador fiscal; revisá también los inactivos.");
        if (row is null) db.Suppliers.Add(new Supplier(target, profile, actor, clock.GetUtcNow().UtcDateTime));
        else row.Update(profile, input.IsActive);
        db.AuditEntries.Add(new AuditEntry { ActorId = actor, Action = id is null ? "supplier.created" : "supplier.updated",
            TargetId = target.ToString(), OccurredAt = clock.GetUtcNow() });
        try { await db.SaveChangesAsync(ct); return new(CatalogResultCode.Success, target); }
        catch (DbUpdateConcurrencyException) { return Conflict(); }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 or 5 or 6 }) { return Conflict(); }
        catch (SqliteException ex) when (ex.SqliteErrorCode is 5 or 6) { return Conflict(); }
    }

    private string Demand(string permission) => current.Id is { Length: > 0 } actor && current.HasPermission(permission)
        ? actor : throw new UnauthorizedAccessException();
    private static CatalogResult Conflict() => new(CatalogResultCode.Conflict,
        Error: "Los datos cambiaron o ya existen. Recargá la ficha antes de guardar.");
    private static SupplierSummary Summary(Supplier row) => new(row.Id, row.Name, row.SuppliesProducts, row.IsLaboratory, row.IsActive);
}
