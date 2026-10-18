using OpticaSosa.Domain.Catalog;

namespace OpticaSosa.Domain.Commerce;

// Cada guardado de borrador conserva sus líneas; sólo la revisión confirmada se utiliza operativamente.
public sealed class SalesLine
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public int DraftNumber { get; private set; }
    public int Position { get; private set; }
    public Guid VariantId { get; private set; }
    public string ProductName { get; private set; } = "";
    public string VariantCode { get; private set; } = "";
    public ProductCategory Category { get; private set; }
    public ProductUnit Unit { get; private set; }
    public int UnitsPerPackage { get; private set; }
    public int Quantity { get; private set; }
    public FulfilmentSource Source { get; private set; }
    public long UnitPriceMinor { get; private set; }
    public long DiscountMinor { get; private set; }
    public string? DiscountReason { get; private set; }
    public long TotalMinor { get; private set; }

    private SalesLine() { }
    public SalesLine(Guid id, Guid order, int draft, int position, ProductVariant variant, string productName,
        int quantity, FulfilmentSource source, Money unitPrice, Money discount, string? discountReason)
    {
        if (id == Guid.Empty || order == Guid.Empty || draft < 1 || position < 1 || quantity < 1 || !Enum.IsDefined(source))
            throw new ArgumentException("Revisá identificación, cantidad y origen de la línea.");
        if (unitPrice.Currency != discount.Currency) throw new ArgumentException("La moneda del descuento debe coincidir con el precio.");
        long gross;
        try { gross = checked(unitPrice.MinorUnits * quantity); }
        catch (OverflowException) { throw new ArgumentException("El importe de la línea excede la capacidad del sistema."); }
        if (discount.MinorUnits > gross) throw new ArgumentException("El descuento no puede superar el importe de la línea.");
        Id = id; OrderId = order; DraftNumber = draft; Position = position; VariantId = variant.Id;
        ProductName = CatalogText.Required(productName, 160, "el producto"); VariantCode = variant.Code;
        Category = variant.Category; Unit = variant.Unit; UnitsPerPackage = variant.UnitsPerPackage;
        Quantity = quantity; Source = source; UnitPriceMinor = unitPrice.MinorUnits; DiscountMinor = discount.MinorUnits;
        DiscountReason = discount.MinorUnits > 0 ? CatalogText.Required(discountReason, 500, "el motivo del descuento") : null;
        TotalMinor = gross - discount.MinorUnits;
    }
}
