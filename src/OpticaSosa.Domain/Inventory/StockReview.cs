using OpticaSosa.Domain.Catalog;

namespace OpticaSosa.Domain.Inventory;

public enum StockReviewerRole { Professional = 1, Sales = 2 }

public sealed class StockReview
{
    public Guid Id { get; private set; }
    public Guid HoldId { get; private set; }
    public int Quantity { get; private set; }
    public bool Approved { get; private set; }
    public bool ProductMatches { get; private set; }
    public bool ItemIntact { get; private set; }
    public bool? SealIntact { get; private set; }
    public string Reason { get; private set; } = "";
    public string RequestHash { get; private set; } = "";
    public string ActorUserId { get; private set; } = "";
    public StockReviewerRole Role { get; private set; }
    public DateTime ReviewedAtUtc { get; private set; }
    private StockReview() { }
    public StockReview(Guid id, StockHold hold, StockBatch batch, int quantity, bool approve, bool productMatches, bool intact,
        bool? sealIntact, string reason, string hash, string actor, StockReviewerRole role, DateTime utcNow, DateOnly today)
    {
        if (id == Guid.Empty || hold.BatchId != batch.Id || quantity <= 0 || quantity > hold.PendingQuantity ||
            string.IsNullOrWhiteSpace(actor) || utcNow.Kind != DateTimeKind.Utc || hash.Length != 64 || !Enum.IsDefined(role) ||
            ((batch.Category is ProductCategory.Lens or ProductCategory.ContactLens) && role != StockReviewerRole.Professional))
            throw new ArgumentException("Revisión de cuarentena inválida.");
        if (approve && (!productMatches || !intact || (batch.ExpiresOn is { } expiry && today > expiry) ||
            (batch.Category == ProductCategory.ContactLens && (hold.OpenedContact || sealIntact != true))))
            throw new ArgumentException("No se puede liberar material incorrecto, dañado, vencido o contactos abiertos/sin precinto íntegro confirmado.");
        Id = id; HoldId = hold.Id; Quantity = quantity; Approved = approve; ProductMatches = productMatches; ItemIntact = intact; SealIntact = sealIntact;
        Reason = CatalogText.Required(reason, 500, "el resultado de revisión"); RequestHash = hash; ActorUserId = actor; Role = role; ReviewedAtUtc = utcNow;
    }
}
