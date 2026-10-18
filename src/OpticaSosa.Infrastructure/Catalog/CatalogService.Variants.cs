using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Catalog;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Catalog;

namespace OpticaSosa.Infrastructure.Catalog;

public sealed partial class CatalogService
{
    public Task<CatalogResult> CreateVariantAsync(Guid productId, VariantInput input, CancellationToken ct = default) => SaveVariantAsync(productId, null, input, ct);
    public Task<CatalogResult> UpdateVariantAsync(Guid productId, Guid id, VariantInput input, CancellationToken ct = default) => SaveVariantAsync(productId, id, input, ct);

    private async Task<CatalogResult> SaveVariantAsync(Guid productId, Guid? id, VariantInput input, CancellationToken ct)
    {
        var actor = CatalogOperations.Demand(current, Permissions.ManageCatalog);
        var target = id ?? input.SubmissionId;
        if (target == Guid.Empty || productId == Guid.Empty || (id is null && !input.IsActive)) return CatalogOperations.Invalid("Identificación o estado inicial inválidos.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var product = await db.Products.SingleOrDefaultAsync(x => x.Id == productId, ct);
        if (product is null) return CatalogOperations.Missing();
        var row = await db.ProductVariants.SingleOrDefaultAsync(x => x.Id == target, ct);
        if (row is not null && row.ProductId != productId) return CatalogOperations.Conflict();
        VariantSpecification spec; Money? price;
        try
        {
            spec = input.ToSpecification(product.Category);
            if (input.SalePrice is null && input.Currency is not null) return CatalogOperations.Invalid("Una moneda requiere un precio explícito.");
            price = input.SalePrice is { } amount ? Money.FromDecimal(amount, input.Currency ?? "") : null;
        }
        catch (ArgumentException ex) { return CatalogOperations.Invalid(ex.Message); }
        if (id is null && row is not null)
            return row.CreatedByUserId == actor && row.IsActive && row.Specification() == spec && row.Price() == price
                ? new(CatalogResultCode.Success, target) : CatalogOperations.Conflict();
        if (id is not null && row is null) return CatalogOperations.Missing();
        if (product.Version != input.ProductVersion || (row is not null && row.Version != input.Version)) return CatalogOperations.Conflict();
        if (!product.IsActive) return CatalogOperations.Invalid("El producto está inactivo.");
        var settings = await db.CommercialSettings.SingleAsync(ct);
        if (price is not null && price.Currency != settings.Currency) return CatalogOperations.Invalid("El precio debe usar la moneda configurada para esta instalación.");
        if (await db.ProductVariants.AnyAsync(x => x.Id != target && x.CodeKey == spec.CodeKey, ct))
            return new(CatalogResultCode.Duplicate, Error: "El código de variante ya existe; revisá también los inactivos.");
        if (row is null) db.ProductVariants.Add(new(target, productId, spec, price, actor, clock.GetUtcNow().UtcDateTime));
        else
        {
            try { row.Update(spec, price, input.IsActive); }
            catch (ArgumentException ex) { return CatalogOperations.Invalid(ex.Message); }
        }
        product.Touch();
        // Serializar también con la configuración de moneda para evitar un cambio concurrente.
        settings.Touch();
        CatalogOperations.Audit(db, actor, id is null ? "variant.created" : "variant.updated", target, clock);
        return await CatalogOperations.SaveAsync(db, target, ct);
    }
}
