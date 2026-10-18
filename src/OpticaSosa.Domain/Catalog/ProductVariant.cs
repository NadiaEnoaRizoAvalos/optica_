namespace OpticaSosa.Domain.Catalog;

public sealed class ProductVariant
{
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public ProductCategory Category { get; private set; }
    public string Code { get; private set; } = "";
    public string CodeKey { get; private set; } = "";
    public ProductUnit Unit { get; private set; }
    public int UnitsPerPackage { get; private set; }
    public string? FrameColor { get; private set; }
    public decimal? FrameEyeSizeMm { get; private set; }
    public decimal? FrameBridgeMm { get; private set; }
    public decimal? FrameTempleMm { get; private set; }
    public string? LensMaterial { get; private set; }
    public decimal? LensRefractiveIndex { get; private set; }
    public string? LensTreatment { get; private set; }
    public decimal? Sphere { get; private set; }
    public decimal? Cylinder { get; private set; }
    public int? Axis { get; private set; }
    public decimal? Addition { get; private set; }
    public decimal? ContactBaseCurveMm { get; private set; }
    public decimal? ContactDiameterMm { get; private set; }
    public long? PriceMinor { get; private set; }
    public string? PriceCurrency { get; private set; }
    public bool IsActive { get; private set; }
    public bool SpecificationLocked { get; private set; }
    public Guid Version { get; private set; }
    public string CreatedByUserId { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }

    private ProductVariant() { }
    public ProductVariant(Guid id, Guid productId, VariantSpecification specification, Money? price, string actor, DateTime utcNow)
    {
        if (id == Guid.Empty || productId == Guid.Empty || string.IsNullOrWhiteSpace(actor) || utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Identificación o fecha inválida.");
        Id = id; ProductId = productId; Category = specification.Category; CreatedByUserId = actor; CreatedAtUtc = utcNow;
        Update(specification, price, true);
    }

    public VariantSpecification Specification() => new(Code, Category, UnitsPerPackage,
        Category == ProductCategory.Frame ? new(FrameColor, FrameEyeSizeMm, FrameBridgeMm, FrameTempleMm) : null,
        Category == ProductCategory.Lens ? new(LensMaterial, LensRefractiveIndex, LensTreatment, Sphere, Cylinder, Axis, Addition) : null,
        Category == ProductCategory.ContactLens ? new(Sphere, Cylinder, Axis, Addition, ContactBaseCurveMm, ContactDiameterMm) : null);
    public Money? Price() => PriceMinor is { } value ? new(value, PriceCurrency!) : null;

    public void Update(VariantSpecification spec, Money? price, bool active)
    {
        if (spec.Category != Category) throw new ArgumentException("La categoría no puede cambiar.");
        if (SpecificationLocked && Specification() != spec)
            throw new ArgumentException("La variante tiene operaciones asociadas; creá otra para cambiar sus especificaciones.");
        Code = spec.Code; CodeKey = spec.CodeKey; Unit = spec.Unit; UnitsPerPackage = spec.UnitsPerPackage;
        FrameColor = spec.Frame?.Color; FrameEyeSizeMm = spec.Frame?.EyeSizeMm; FrameBridgeMm = spec.Frame?.BridgeMm; FrameTempleMm = spec.Frame?.TempleMm;
        LensMaterial = spec.Lens?.Material; LensRefractiveIndex = spec.Lens?.RefractiveIndex; LensTreatment = spec.Lens?.Treatment;
        Sphere = spec.Lens?.Sphere ?? spec.Contact?.Sphere; Cylinder = spec.Lens?.Cylinder ?? spec.Contact?.Cylinder;
        Axis = spec.Lens?.Axis ?? spec.Contact?.Axis; Addition = spec.Lens?.Addition ?? spec.Contact?.Addition;
        ContactBaseCurveMm = spec.Contact?.BaseCurveMm; ContactDiameterMm = spec.Contact?.DiameterMm;
        PriceMinor = price?.MinorUnits; PriceCurrency = price?.Currency; IsActive = active; Version = Guid.NewGuid();
    }

    public void LockSpecification() { SpecificationLocked = true; Version = Guid.NewGuid(); }
}
