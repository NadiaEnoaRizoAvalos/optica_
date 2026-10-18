using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Catalog;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Catalog;

public sealed class TechnicalCatalog(IDbContextFactory<OpticaDbContext> factory, ICurrentUser current) : ITechnicalCatalog
{
    public async Task<ProductPage> SearchAsync(string? search, ProductCategory? category = null, int page = 1, CancellationToken ct = default)
    {
        CatalogOperations.Demand(current, Permissions.ReadTechnicalCatalog);
        await using var db = await factory.CreateDbContextAsync(ct);
        return await CatalogOperations.SearchAsync(db, search, category, false, page, ct);
    }
    public async Task<TechnicalVariantPage> VariantsAsync(Guid productId, int page = 1, CancellationToken ct = default)
    {
        CatalogOperations.Demand(current, Permissions.ReadTechnicalCatalog); CatalogOperations.Page(page);
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await db.ProductVariants.AsNoTracking().Where(x => x.ProductId == productId && x.IsActive &&
                db.Products.Any(p => p.Id == x.ProductId && p.IsActive))
            .OrderBy(x => x.CodeKey).ThenBy(x => x.Id).Skip((page - 1) * 50).Take(51).ToListAsync(ct);
        return new(rows.Take(50).Select(CatalogOperations.Technical).ToArray(), page, rows.Count > 50);
    }
    public async Task<TechnicalVariant?> VariantAsync(Guid productId, Guid id, CancellationToken ct = default)
    {
        CatalogOperations.Demand(current, Permissions.ReadTechnicalCatalog);
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.ProductVariants.AsNoTracking().SingleOrDefaultAsync(x => x.ProductId == productId && x.Id == id && x.IsActive &&
            db.Products.Any(p => p.Id == x.ProductId && p.IsActive), ct);
        return row is null ? null : CatalogOperations.Technical(row);
    }
}
