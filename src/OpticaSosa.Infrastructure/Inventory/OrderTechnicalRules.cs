using Microsoft.EntityFrameworkCore;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Clinical;
using OpticaSosa.Domain.Commerce;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Domain.Technical;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Inventory;

internal sealed record PublishedTechnical(TechnicalPublication Publication, Guid CustomerId, int RevisionNumber, int CurrentNumber, bool CustomerActive)
{
    internal PublicationStatus Status => Publication.Status(RevisionNumber, CurrentNumber, CustomerActive);
}
internal static class OrderTechnicalRules
{
    internal static bool Required(ProductCategory category) => category is ProductCategory.Lens or ProductCategory.ContactLens;
    internal static Task<PublishedTechnical?> LoadAsync(OpticaDbContext db, Guid id, CancellationToken ct) =>
        (from publication in db.TechnicalPublications.AsNoTracking()
         join revision in db.ClinicalRevisions.AsNoTracking() on publication.SourceRevisionId equals revision.Id
         join record in db.ClinicalRecords.AsNoTracking() on revision.RecordId equals record.Id
         join customer in db.Customers.AsNoTracking() on record.CustomerId equals customer.Id
         where publication.Id == id
         select new PublishedTechnical(publication, customer.Id, revision.Number, record.RevisionNumber, customer.IsActive)).SingleOrDefaultAsync(ct);

    internal static string? Validate(SalesLine line, ProductVariant variant, TechnicalPublication publication, TechnicalEyes eyes)
    {
        var request = publication.Request;
        if (!Required(line.Category) || request.RequestedCategory != line.Category || eyes is not (TechnicalEyes.Right or TechnicalEyes.Left or TechnicalEyes.Both))
            return "Indicá una autorización correspondiente al producto y a los ojos del trabajo.";
        var right = eyes.HasFlag(TechnicalEyes.Right); var left = eyes.HasFlag(TechnicalEyes.Left);
        if ((right && !request.IncludeRight) || (left && !request.IncludeLeft)) return "La publicación no autoriza todos los ojos seleccionados.";
        if (request.ProductId is { } product && product != variant.ProductId) return "El producto no coincide con el autorizado.";
        if (line.Category == ProductCategory.Lens && eyes == TechnicalEyes.Both && line.Quantity < 2) return "Ambos ojos necesitan al menos dos cristales individuales.";
        foreach (var (included, values, selection) in new[] { (right, publication.Right, request.RightContact), (left, publication.Left, request.LeftContact) })
        {
            if (!included) continue;
            if (line.Category == ProductCategory.ContactLens && selection?.VariantId != variant.Id) return "La caja no coincide con el contacto autorizado para cada ojo incluido.";
            if (line.Category == ProductCategory.Lens)
            {
                if ((request.Material is not null && CatalogText.Key(request.Material) != CatalogText.Key(variant.LensMaterial ?? "")) ||
                    (request.RefractiveIndex is { } index && index != variant.LensRefractiveIndex) ||
                    (request.Treatment is not null && CatalogText.Key(request.Treatment) != CatalogText.Key(variant.LensTreatment ?? "")))
                    return "Material, índice o tratamiento no coinciden con lo autorizado.";
                if (line.Source == FulfilmentSource.Stock && (!SamePowers(variant, values) || values.Prism is not (null or 0)))
                    return "El cristal de stock no describe exactamente la graduación autorizada (incluido prisma); requiere otra selección o fabricación validada.";
            }
        }
        return null;
    }
    private static bool SamePowers(ProductVariant v, EyeValues p) => v.Sphere == p.Sphere && v.Cylinder == p.Cylinder &&
        v.Addition == p.Addition && (p.Cylinder is null or 0 || v.Axis == p.Axis);
}
