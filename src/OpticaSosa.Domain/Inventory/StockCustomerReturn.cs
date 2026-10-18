using OpticaSosa.Domain.Catalog;

namespace OpticaSosa.Domain.Inventory;

public sealed class StockCustomerReturn
{
    public Guid Id { get; private set; }
    public Guid ReservationId { get; private set; }
    public Guid HoldId { get; private set; }
    public int Quantity { get; private set; }
    public bool? ContactPackageOpened { get; private set; }
    public string Reason { get; private set; } = "";
    public string ActorUserId { get; private set; } = "";
    public DateTime ReturnedAtUtc { get; private set; }
    private StockCustomerReturn() { }
    public StockCustomerReturn(Guid id, Guid holdId, StockReservation reservation, ProductCategory category, int quantity,
        bool? contactPackageOpened, string reason, string actor, DateTime utcNow)
    {
        if (id == Guid.Empty || holdId == Guid.Empty || reservation.Status != ReservationStatus.Consumed || quantity <= 0 || quantity > reservation.Quantity ||
            string.IsNullOrWhiteSpace(actor) || utcNow.Kind != DateTimeKind.Utc ||
            (category == ProductCategory.ContactLens ? contactPackageOpened is null : contactPackageOpened is not null))
            throw new ArgumentException("La devolución requiere material efectivamente consumido/entregado y el estado del envase cuando es contacto.");
        Id = id; HoldId = holdId; ReservationId = reservation.Id; Quantity = quantity; ContactPackageOpened = contactPackageOpened;
        Reason = CatalogText.Required(reason, 500, "el motivo de devolución"); ActorUserId = actor; ReturnedAtUtc = utcNow;
    }
}
