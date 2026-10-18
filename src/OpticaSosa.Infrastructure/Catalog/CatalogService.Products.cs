using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Catalog;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Catalog;

namespace OpticaSosa.Infrastructure.Catalog;

public sealed partial class CatalogService
{
    public Task<CatalogResult> CreateAsync(ProductInput input, CancellationToken ct = default) => SaveProductAsync(null, input, ct);
    public Task<CatalogResult> UpdateAsync(Guid id, ProductInput input, CancellationToken ct = default) => SaveProductAsync(id, input, ct);

    private async Task<CatalogResult> SaveProductAsync(Guid? id, ProductInput input, CancellationToken ct)
    {
        var actor = CatalogOperations.Demand(current, Permissions.ManageCatalog);
        ProductProfile profile;
        try { profile = input.ToProfile(); }
        catch (ArgumentException ex) { return CatalogOperations.Invalid(ex.Message); }
        var target = id ?? input.SubmissionId;
        if (target == Guid.Empty || (id is null && !input.IsActive) || input.SupplierIds is null || input.SupplierIds.Any(x => x == Guid.Empty) ||
            input.SupplierIds.Distinct().Count() != input.SupplierIds.Count)
            return CatalogOperations.Invalid("Recargá el formulario y revisá los proveedores seleccionados.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.Products.SingleOrDefaultAsync(x => x.Id == target, ct);
        var links = await db.ProductSuppliers.Where(x => x.ProductId == target).ToListAsync(ct);
        if (id is null && row is not null)
            return row.CreatedByUserId == actor && row.IsActive && row.Matches(profile) &&
                links.Where(x => x.IsActive).Select(x => x.SupplierId).Order().SequenceEqual(input.SupplierIds.Order())
                ? new(CatalogResultCode.Success, target) : CatalogOperations.Conflict();
        if (id is not null && row is null) return CatalogOperations.Missing();
        if (row is not null && row.Version != input.Version) return CatalogOperations.Conflict();
        if (row is not null && row.Category != profile.Category) return CatalogOperations.Invalid("La categoría de un producto existente no puede cambiar.");
        var name = CatalogText.Key(profile.Name); var brand = CatalogText.Key(profile.Brand ?? ""); var model = CatalogText.Key(profile.Model ?? "");
        if (await db.Products.AnyAsync(x => x.Id != target && x.Category == profile.Category && x.NameKey == name && x.BrandKey == brand && x.ModelKey == model, ct))
            return new(CatalogResultCode.Duplicate, Error: "Ya existe el producto; revisá también los inactivos y sus variantes.");
        var supplierIds = input.SupplierIds.ToArray();
        var suppliers = await db.Suppliers.Where(x => supplierIds.Contains(x.Id)).ToListAsync(ct);
        if (suppliers.Count != supplierIds.Length || suppliers.Any(x => !x.IsActive && !links.Any(l => l.SupplierId == x.Id && l.IsActive)))
            return CatalogOperations.Invalid("Seleccioná proveedores existentes y activos para nuevas asociaciones.");
        if (row is null) db.Products.Add(new(target, profile, actor, clock.GetUtcNow().UtcDateTime));
        else row.Update(profile, input.IsActive);
        foreach (var supplier in suppliers.Where(x => x.IsActive)) supplier.Touch();
        foreach (var link in links) link.SetActive(supplierIds.Contains(link.SupplierId));
        foreach (var supplier in supplierIds.Where(x => links.All(l => l.SupplierId != x))) db.ProductSuppliers.Add(new(target, supplier));
        CatalogOperations.Audit(db, actor, id is null ? "product.created" : "product.updated", target, clock);
        return await CatalogOperations.SaveAsync(db, target, ct);
    }
}
