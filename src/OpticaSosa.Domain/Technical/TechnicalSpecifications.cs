using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Clinical;

namespace OpticaSosa.Domain.Technical;

public enum TechnicalPurpose { SpectacleLenses = 1, ContactLenses = 2 }
public enum PublicationStatus { Current, Revoked, Superseded, SourceChanged, CustomerInactive }

public sealed record MountingSpecification
{
    public decimal? RightHeightMm { get; private set; }
    public decimal? LeftHeightMm { get; private set; }
    public decimal? RightMonocularDistanceMm { get; private set; }
    public decimal? LeftMonocularDistanceMm { get; private set; }
    public Guid? FrameVariantId { get; private set; }
    public string? FrameReference { get; private set; }
    public string? Instructions { get; private set; }
    public MountingSpecification(decimal? rightHeightMm = null, decimal? leftHeightMm = null,
        decimal? rightMonocularDistanceMm = null, decimal? leftMonocularDistanceMm = null,
        Guid? frameVariantId = null, string? frameReference = null, string? instructions = null)
    {
        RightHeightMm = Measure(rightHeightMm); LeftHeightMm = Measure(leftHeightMm);
        RightMonocularDistanceMm = Measure(rightMonocularDistanceMm); LeftMonocularDistanceMm = Measure(leftMonocularDistanceMm);
        if (frameVariantId == Guid.Empty) throw new ArgumentException("Referencia de armazón inválida.");
        FrameVariantId = frameVariantId; FrameReference = CatalogText.Optional(frameReference, 200);
        Instructions = CatalogText.Optional(instructions, 1000);
    }
    private static decimal? Measure(decimal? value)
    {
        if (value < 0) throw new ArgumentException("Las medidas de montaje no pueden ser negativas.");
        EyeMeasurement.Scale(value, 100, "montaje en mm"); return value;
    }
}

public sealed record ContactSelection
{
    public Guid VariantId { get; private set; }
    public decimal BaseCurveMm { get; private set; }
    public decimal DiameterMm { get; private set; }
    private ContactSelection() { }
    public ContactSelection(Guid variantId, decimal baseCurveMm, decimal diameterMm)
    {
        if (variantId == Guid.Empty || baseCurveMm <= 0 || diameterMm <= 0) throw new ArgumentException("Indicá variante, curva base y diámetro válidos.");
        EyeMeasurement.Scale(baseCurveMm, 100, "curva base"); EyeMeasurement.Scale(diameterMm, 100, "diámetro");
        VariantId = variantId; BaseCurveMm = baseCurveMm; DiameterMm = diameterMm;
    }
}

public sealed record TechnicalRequest
{
    public TechnicalPurpose Purpose { get; private set; }
    public ProductCategory RequestedCategory { get; private set; }
    public bool IncludeRight { get; private set; }
    public bool IncludeLeft { get; private set; }
    public string Product { get; private set; } = "";
    public Guid? ProductId { get; private set; }
    public string? Material { get; private set; }
    public decimal? RefractiveIndex { get; private set; }
    public string? Treatment { get; private set; }
    public MountingSpecification Mounting { get; private set; } = new();
    public ContactSelection? RightContact { get; private set; }
    public ContactSelection? LeftContact { get; private set; }
    private TechnicalRequest() { }
    public TechnicalRequest(TechnicalPurpose purpose, bool includeRight, bool includeLeft, string product,
        Guid? productId = null, string? material = null, decimal? refractiveIndex = null, string? treatment = null,
        MountingSpecification? mounting = null, ContactSelection? rightContact = null, ContactSelection? leftContact = null)
    {
        if (!Enum.IsDefined(purpose) || (!includeRight && !includeLeft) || productId == Guid.Empty)
            throw new ArgumentException("Indicá el destino técnico y al menos un ojo incluido.");
        Purpose = purpose; IncludeRight = includeRight; IncludeLeft = includeLeft;
        RequestedCategory = purpose == TechnicalPurpose.SpectacleLenses ? ProductCategory.Lens : ProductCategory.ContactLens;
        Product = CatalogText.Required(product, 200, "el producto solicitado"); ProductId = productId;
        Material = CatalogText.Optional(material, 100); Treatment = CatalogText.Optional(treatment, 160);
        if (refractiveIndex <= 0) throw new ArgumentException("El índice debe ser positivo.");
        EyeMeasurement.Scale(refractiveIndex, 1000, "índice"); RefractiveIndex = refractiveIndex;
        // Los valores pueden coincidir, pero cada navegación owned de EF necesita su propia instancia.
        Mounting = mounting is null ? new() : mounting with { };
        RightContact = rightContact is null ? null : rightContact with { };
        LeftContact = leftContact is null ? null : leftContact with { };
        if ((!includeRight && (Mounting.RightHeightMm is not null || Mounting.RightMonocularDistanceMm is not null || rightContact is not null)) ||
            (!includeLeft && (Mounting.LeftHeightMm is not null || Mounting.LeftMonocularDistanceMm is not null || leftContact is not null)))
            throw new ArgumentException("No se pueden informar datos para un ojo excluido.");
        if (purpose == TechnicalPurpose.ContactLenses)
        {
            if ((includeRight && rightContact is null) || (includeLeft && leftContact is null) || Mounting != new MountingSpecification())
                throw new ArgumentException("Confirmá producto, curva base y diámetro por ojo; el montaje de armazón corresponde a cristales.");
        }
        else if (rightContact is not null || leftContact is not null) throw new ArgumentException("Los parámetros de contacto no corresponden a cristales.");
    }
}
