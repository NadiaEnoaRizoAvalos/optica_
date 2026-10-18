using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Catalog;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Infrastructure.Identity;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Catalog;

internal static class CatalogOperations
{
    internal static string Demand(ICurrentUser current, string permission) => current.Id is { Length: > 0 } actor && current.HasPermission(permission)
        ? actor : throw new UnauthorizedAccessException();
    internal static void Page(int page)
    {
        if (page < 1 || page > int.MaxValue / 50) throw new ArgumentOutOfRangeException(nameof(page));
    }
    internal static ProductSummary Summary(Product row) => new(row.Id, row.Name, row.Category, row.Brand, row.Model, row.IsActive, row.Version);
    internal static TechnicalVariant Technical(ProductVariant row) => new(row.Id, row.ProductId, row.Specification(), row.IsActive, row.SpecificationLocked, row.Version);
    internal static void Audit(OpticaDbContext db, string actor, string action, Guid? id, TimeProvider clock) =>
        db.AuditEntries.Add(new AuditEntry { ActorId = actor, Action = action, TargetId = id?.ToString(), OccurredAt = clock.GetUtcNow() });
    internal static CatalogResult Conflict() => new(CatalogResultCode.Conflict, Error: "Los datos cambiaron o ya existen. Recargá antes de guardar.");
    internal static CatalogResult Invalid(string message) => new(CatalogResultCode.Invalid, Error: message);
    internal static CatalogResult Missing() => new(CatalogResultCode.NotFound, Error: "No se encontró el producto o la variante.");
    internal static async Task<CatalogResult> SaveAsync(OpticaDbContext db, Guid? id, CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return new(CatalogResultCode.Success, id); }
        catch (DbUpdateConcurrencyException) { return Conflict(); }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 or 5 or 6 }) { return Conflict(); }
        catch (SqliteException ex) when (ex.SqliteErrorCode is 5 or 6) { return Conflict(); }
    }

    internal static async Task<ProductPage> SearchAsync(OpticaDbContext db, string? search, ProductCategory? category, bool includeInactive, int page, CancellationToken ct)
    {
        Page(page);
        if (category is { } kind && !Enum.IsDefined(kind)) throw new ArgumentException("Categoría inválida.");
        var key = CatalogText.Key(CatalogText.Optional(search, 160) ?? "");
        var query = db.Products.AsNoTracking().Where(x => (includeInactive || x.IsActive) && (category == null || x.Category == category) &&
            (key == "" || x.NameKey.Contains(key) || x.BrandKey.Contains(key) || x.ModelKey.Contains(key) ||
             db.ProductVariants.Any(v => v.ProductId == x.Id && (includeInactive || v.IsActive) && v.CodeKey.Contains(key))));
        var rows = await query.OrderBy(x => x.NameKey).ThenBy(x => x.Id).Skip((page - 1) * 50).Take(51).ToListAsync(ct);
        return new(rows.Take(50).Select(Summary).ToArray(), page, rows.Count > 50);
    }
}
