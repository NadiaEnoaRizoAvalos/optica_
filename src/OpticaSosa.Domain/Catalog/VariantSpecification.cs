namespace OpticaSosa.Domain.Catalog;

public sealed record FrameSpecification(string? Color = null, decimal? EyeSizeMm = null, decimal? BridgeMm = null, decimal? TempleMm = null);
public sealed record LensSpecification(string? Material = null, decimal? RefractiveIndex = null, string? Treatment = null,
    decimal? Sphere = null, decimal? Cylinder = null, int? Axis = null, decimal? Addition = null);
public sealed record ContactSpecification(decimal? Sphere = null, decimal? Cylinder = null, int? Axis = null,
    decimal? Addition = null, decimal? BaseCurveMm = null, decimal? DiameterMm = null);

public sealed record VariantSpecification
{
    public string Code { get; }
    public string CodeKey => CatalogText.Key(Code);
    public ProductCategory Category { get; }
    public ProductUnit Unit => Category == ProductCategory.ContactLens ? ProductUnit.ClosedBox : ProductUnit.Each;
    public int UnitsPerPackage { get; }
    public FrameSpecification? Frame { get; }
    public LensSpecification? Lens { get; }
    public ContactSpecification? Contact { get; }

    public VariantSpecification(string code, ProductCategory category, int? unitsPerPackage = null,
        FrameSpecification? frame = null, LensSpecification? lens = null, ContactSpecification? contact = null)
    {
        Code = CatalogText.Required(code, 80, "el código de la variante"); Category = category;
        if (!Enum.IsDefined(category)) throw new ArgumentException("Categoría inválida.");
        if ((frame is not null && category != ProductCategory.Frame) || (lens is not null && category != ProductCategory.Lens) ||
            (contact is not null && category != ProductCategory.ContactLens)) throw new ArgumentException("Los atributos no corresponden a la categoría.");
        if (category == ProductCategory.ContactLens)
        {
            if (unitsPerPackage is null or < 1) throw new ArgumentException("Indicá cuántos lentes contiene la caja cerrada.");
            UnitsPerPackage = unitsPerPackage.Value;
            contact ??= new();
            Contact = new(Power(contact.Sphere), Power(contact.Cylinder), AxisValue(contact.Axis), Power(contact.Addition),
                Positive(contact.BaseCurveMm, 100), Positive(contact.DiameterMm, 100));
        }
        else
        {
            if (unitsPerPackage is not null and not 1) throw new ArgumentException("Esta categoría se administra por unidad individual.");
            UnitsPerPackage = 1;
        }
        if (category == ProductCategory.Frame)
        {
            frame ??= new();
            Frame = new(CatalogText.Optional(frame.Color, 80), Positive(frame.EyeSizeMm, 100), Positive(frame.BridgeMm, 100), Positive(frame.TempleMm, 100));
        }
        if (category == ProductCategory.Lens)
        {
            lens ??= new();
            Lens = new(CatalogText.Optional(lens.Material, 100), Positive(lens.RefractiveIndex, 1000), CatalogText.Optional(lens.Treatment, 160),
                Power(lens.Sphere), Power(lens.Cylinder), AxisValue(lens.Axis), Power(lens.Addition));
        }
    }

    private static int? AxisValue(int? value) => value is < 0 or > 180 ? throw new ArgumentException("El eje debe estar entre 0 y 180 grados.") : value;
    private static decimal? Power(decimal? value) => Exact(value, 1000);
    private static decimal? Positive(decimal? value, int scale) => value <= 0
        ? throw new ArgumentException("La medida debe ser positiva o quedar sin informar.") : Exact(value, scale);
    private static decimal? Exact(decimal? value, int scale)
    {
        if (value is null) return null;
        if (value < int.MinValue / (decimal)scale || value > int.MaxValue / (decimal)scale || decimal.Truncate(value.Value * scale) != value.Value * scale)
            throw new ArgumentException("La medida excede la precisión o capacidad del campo.");
        return value;
    }
}
