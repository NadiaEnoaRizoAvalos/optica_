using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Catalog;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Catalog;

public sealed partial class CatalogService(IDbContextFactory<OpticaDbContext> factory, ICurrentUser current, TimeProvider clock) : ICatalogService
{
    public async Task<ProductPage> SearchAsync(string? search, ProductCategory? category = null, bool includeInactive = false, int page = 1, CancellationToken ct = default)
    {
        CatalogOperations.Demand(current, Permissions.ReadCatalog);
        await using var db = await factory.CreateDbContextAsync(ct);
        return await CatalogOperations.SearchAsync(db, search, category, includeInactive, page, ct);
    }
    public async Task<ProductDetails?> GetAsync(Guid id, CancellationToken ct = default)
    {
        CatalogOperations.Demand(current, Permissions.ReadCatalog);
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return null;
        var suppliers = await (from link in db.ProductSuppliers.AsNoTracking()
            join supplier in db.Suppliers.AsNoTracking() on link.SupplierId equals supplier.Id
            where link.ProductId == id && link.IsActive orderby supplier.NameKey, supplier.Id
            select new SupplierSummary(supplier.Id, supplier.Name, supplier.SuppliesProducts, supplier.IsLaboratory, supplier.IsActive)).ToArrayAsync(ct);
        return new(CatalogOperations.Summary(row), row.Description, suppliers);
    }
    public async Task<VariantPage> VariantsAsync(Guid productId, bool includeInactive = false, int page = 1, CancellationToken ct = default)
    {
        CatalogOperations.Demand(current, Permissions.ReadCatalog); CatalogOperations.Page(page);
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await db.ProductVariants.AsNoTracking().Where(x => x.ProductId == productId && (includeInactive || x.IsActive))
            .OrderBy(x => x.CodeKey).ThenBy(x => x.Id).Skip((page - 1) * 50).Take(51).ToListAsync(ct);
        return new(rows.Take(50).Select(x => new VariantDetails(CatalogOperations.Technical(x), x.Price())).ToArray(), page, rows.Count > 50);
    }
    public async Task<VariantDetails?> VariantAsync(Guid productId, Guid id, CancellationToken ct = default)
    {
        CatalogOperations.Demand(current, Permissions.ReadCatalog);
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.ProductVariants.AsNoTracking().SingleOrDefaultAsync(x => x.ProductId == productId && x.Id == id, ct);
        return row is null ? null : new(CatalogOperations.Technical(row), row.Price());
    }
    public async Task<CommercialConfiguration> ConfigurationAsync(CancellationToken ct = default)
    {
        CatalogOperations.Demand(current, Permissions.ReadCatalog);
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.CommercialSettings.AsNoTracking().SingleAsync(ct);
        return new(row.Currency, row.Version);
    }
    public async Task<CatalogResult> ConfigureCurrencyAsync(string currency, Guid version, CancellationToken ct = default)
    {
        var actor = CatalogOperations.Demand(current, Permissions.ManageCatalog);
        string code;
        try { code = Money.CurrencyCode(currency); }
        catch (ArgumentException ex) { return CatalogOperations.Invalid(ex.Message); }
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.CommercialSettings.SingleAsync(ct);
        if (row.Version != version) return CatalogOperations.Conflict();
        if (row.Currency == code) return new(CatalogResultCode.Success);
        if (await db.ProductVariants.AnyAsync(x => x.PriceCurrency != null && x.PriceCurrency != code, ct))
            return CatalogOperations.Invalid("Hay precios en otra moneda. Se requiere un procedimiento explícito de cambio; no se convierten ni renombran importes automáticamente.");
        row.SetCurrency(code);
        CatalogOperations.Audit(db, actor, "catalog.currency.changed", null, clock);
        return await CatalogOperations.SaveAsync(db, null, ct);
    }
}
