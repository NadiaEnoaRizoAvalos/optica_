using OpticaSosa.Domain.Catalog;

namespace OpticaSosa.Domain.Inventory;

public enum StockHoldOrigin { Receipt = 1, Observation = 2, CustomerReturn = 3, Adjustment = 4 }

public sealed class StockHold
{
    public Guid Id { get; private set; }
    public Guid BatchId { get; private set; }
    public StockHoldOrigin Origin { get; private set; }
    public int Quantity { get; private set; }
    public int ReleasedQuantity { get; private set; }
    public int RemovedQuantity { get; private set; }
    public bool OpenedContact { get; private set; }
    public string Reason { get; private set; } = "";
    public string CreatedByUserId { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }
    public Guid Version { get; private set; }
    public int PendingQuantity => Quantity - ReleasedQuantity - RemovedQuantity;
    private StockHold() { }
    public StockHold(Guid id, Guid batch, StockHoldOrigin origin, int quantity, bool openedContact, string reason, string actor, DateTime utcNow)
    {
        if (id == Guid.Empty || batch == Guid.Empty || !Enum.IsDefined(origin) || quantity <= 0 || string.IsNullOrWhiteSpace(actor) || utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Ingreso a cuarentena inválido.");
        Id = id; BatchId = batch; Origin = origin; Quantity = quantity; OpenedContact = openedContact;
        Reason = CatalogText.Required(reason, 500, "el motivo de cuarentena"); CreatedByUserId = actor; CreatedAtUtc = utcNow; Version = Guid.NewGuid();
    }
    public void Release(int quantity)
    {
        if (OpenedContact) throw new ArgumentException("Un contacto abierto no puede volver al stock utilizable.");
        ValidateQuantity(quantity); ReleasedQuantity = checked(ReleasedQuantity + quantity); Version = Guid.NewGuid();
    }
    public void Remove(int quantity)
    {
        ValidateQuantity(quantity); RemovedQuantity = checked(RemovedQuantity + quantity); Version = Guid.NewGuid();
    }
    public void RecordRejection() => Version = Guid.NewGuid();
    private void ValidateQuantity(int quantity)
    {
        if (quantity <= 0 || quantity > PendingQuantity) throw new ArgumentException("La cantidad supera lo pendiente de esta cuarentena.");
    }
}
