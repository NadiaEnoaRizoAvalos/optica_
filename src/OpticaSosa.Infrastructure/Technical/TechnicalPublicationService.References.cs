using Microsoft.EntityFrameworkCore;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Clinical;
using OpticaSosa.Domain.Technical;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Technical;

public sealed partial class TechnicalPublicationService
{
    private static async Task<string?> ValidateReferencesAsync(OpticaDbContext db, TechnicalPublication publication, CancellationToken ct)
    {
        var request = publication.Request;
        var productIds = new HashSet<Guid>();
        if (request.ProductId is { } productId)
        {
            var product = await db.Products.SingleOrDefaultAsync(x => x.Id == productId && x.IsActive && x.Category == request.RequestedCategory, ct);
            if (product is null) return "El producto solicitado no está activo o no corresponde al destino técnico.";
            productIds.Add(product.Id);
        }
        var variantIds = new[] { request.Mounting.FrameVariantId, request.RightContact?.VariantId, request.LeftContact?.VariantId }
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        var variants = await db.ProductVariants.Where(x => variantIds.Contains(x.Id) && x.IsActive && db.Products.Any(p => p.Id == x.ProductId && p.IsActive)).ToListAsync(ct);
        if (variants.Count != variantIds.Length) return "Seleccioná variantes activas y existentes.";
        if (request.Mounting.FrameVariantId is { } frame && variants.Single(x => x.Id == frame).Category != ProductCategory.Frame)
            return "La referencia de armazón no corresponde a un armazón.";
        foreach (var (selection, powers) in new[] { (request.RightContact, publication.Right), (request.LeftContact, publication.Left) })
        {
            if (selection is null) continue;
            var variant = variants.Single(x => x.Id == selection.VariantId);
            if (variant.Category != ProductCategory.ContactLens || (request.ProductId is not null && request.ProductId != variant.ProductId) ||
                !MatchesContact(variant, selection, powers)) return "El contacto seleccionado no coincide con producto, graduación, curva base o diámetro autorizados.";
        }
        foreach (var variant in variants) { variant.LockSpecification(); productIds.Add(variant.ProductId); }
        var products = await db.Products.Where(x => productIds.Contains(x.Id)).ToListAsync(ct);
        foreach (var product in products) product.Touch();
        return null;
    }
    private static bool MatchesContact(ProductVariant variant, ContactSelection fit, EyeValues powers) =>
        powers.Prism is null or 0 && variant.Sphere == powers.Sphere && variant.Cylinder == powers.Cylinder && variant.Addition == powers.Addition &&
        (powers.Cylinder is null or 0 || variant.Axis == powers.Axis) && variant.ContactBaseCurveMm == fit.BaseCurveMm && variant.ContactDiameterMm == fit.DiameterMm;
}
