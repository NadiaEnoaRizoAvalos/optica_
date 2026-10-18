using OpticaSosa.Domain.Catalog;

namespace OpticaSosa.Domain.Inventory;

public enum ExpiryPrecision { None = 0, Day = 1, Month = 2 }
public enum StockMovementKind { Receipt = 1, Reserve = 2, Release = 3, Consume = 4, Adjustment = 5, WriteOff = 6, Quarantine = 7, ReviewRelease = 8, CustomerReturn = 9, SupplierReturn = 10 }
public enum ReservationStatus { Active = 1, Released = 2, Consumed = 3 }

public sealed class StockBatch
{
    public Guid Id { get; private set; }
    public Guid VariantId { get; private set; }
    public ProductCategory Category { get; private set; }
    public string? Lot { get; private set; }
    public string? LotKey { get; private set; }
    public DateOnly? ExpiresOn { get; private set; }
    public ExpiryPrecision ExpiryPrecision { get; private set; }
    public int OnHand { get; private set; }
    public int Reserved { get; private set; }
    public int Quarantined { get; private set; }
    public string ReceivedByUserId { get; private set; } = "";
    public DateTime ReceivedAtUtc { get; private set; }
    public Guid Version { get; private set; }
    public int Available(DateOnly today) => ExpiresOn is { } expiry && today > expiry ? 0 : OnHand - Quarantined - Reserved;

    private StockBatch() { }
    public StockBatch(Guid id, ProductVariant variant, int quantity, int quarantined, string? lot, DateOnly? expiry,
        ExpiryPrecision precision, string actor, DateTime utcNow)
    {
        if (id == Guid.Empty || quantity <= 0 || quarantined < 0 || quarantined > quantity || string.IsNullOrWhiteSpace(actor) || utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Revisá cantidad, cuarentena, identificación y responsable de recepción.");
        if (!Enum.IsDefined(precision) || ((expiry is null) != (precision == ExpiryPrecision.None)))
            throw new ArgumentException("Indicá fecha y precisión del vencimiento, sin inventar un valor ausente.");
        Id = id; VariantId = variant.Id; Category = variant.Category;
        Lot = CatalogText.Optional(lot, 100); LotKey = Lot is null ? null : CatalogText.Key(Lot);
        if (Category == ProductCategory.ContactLens && (Lot is null || expiry is null))
            throw new ArgumentException("Los contactos necesitan lote y vencimiento de su envase.");
        ExpiresOn = expiry is { } date && precision == ExpiryPrecision.Month
            ? new(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month)) : expiry;
        ExpiryPrecision = precision; OnHand = quantity; Quarantined = quarantined;
        ReceivedByUserId = actor; ReceivedAtUtc = utcNow; Version = Guid.NewGuid();
    }
    public void Reserve(int quantity, DateOnly today)
    {
        if (quantity <= 0 || quantity > Available(today)) throw new ArgumentException("No hay existencias recibidas, utilizables y vigentes suficientes.");
        Reserved = checked(Reserved + quantity); Touch();
    }
    public void Release(int quantity)
    {
        if (quantity <= 0 || quantity > Reserved) throw new ArgumentException("La liberación excede la reserva existente.");
        Reserved -= quantity; Touch();
    }
    public void Consume(int quantity, DateOnly today)
    {
        if (quantity <= 0 || quantity > Reserved || quantity > OnHand - Quarantined || (ExpiresOn is { } expiry && today > expiry))
            throw new ArgumentException("No se puede consumir material no reservado, observado o vencido.");
        OnHand -= quantity; Reserved -= quantity; Touch();
    }
    public void Adjust(int physicalDelta, int quarantineDelta)
    {
        int onHand, quarantine;
        try { onHand = checked(OnHand + physicalDelta); quarantine = checked(Quarantined + quarantineDelta); }
        catch (OverflowException) { throw new ArgumentException("La cantidad excede la capacidad del sistema."); }
        if ((physicalDelta == 0 && quarantineDelta == 0) || onHand < 0 || quarantine < 0 || (long)Reserved + quarantine > onHand)
            throw new ArgumentException("El movimiento no puede retirar reservas ni ocultar diferencias en cuarentena.");
        OnHand = onHand; Quarantined = quarantine; Touch();
    }
    private void Touch() => Version = Guid.NewGuid();
}
