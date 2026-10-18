using OpticaSosa.Domain.Catalog;

namespace OpticaSosa.Domain.Inventory;

public sealed class StockMovement
{
    public Guid Id { get; private set; }
    public Guid BatchId { get; private set; }
    public Guid? ReservationId { get; private set; }
    public StockMovementKind Kind { get; private set; }
    public int PhysicalDelta { get; private set; }
    public int ReservedDelta { get; private set; }
    public int QuarantineDelta { get; private set; }
    public string Reason { get; private set; } = "";
    public string RequestHash { get; private set; } = "";
    public string ActorUserId { get; private set; } = "";
    public DateTime OccurredAtUtc { get; private set; }
    private StockMovement() { }
    public StockMovement(Guid id, Guid batch, Guid? reservation, StockMovementKind kind, int physical, int reserved,
        int quarantine, string reason, string hash, string actor, DateTime utcNow)
    {
        if (id == Guid.Empty || batch == Guid.Empty || reservation == Guid.Empty || !Enum.IsDefined(kind) ||
            hash.Length != 64 || hash.Any(x => !Uri.IsHexDigit(x)) || string.IsNullOrWhiteSpace(actor) || utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Movimiento inválido.");
        var valid = kind switch
        {
            StockMovementKind.Receipt => reservation is null && physical > 0 && reserved == 0 && quarantine >= 0 && quarantine <= physical,
            StockMovementKind.Reserve => reservation is not null && physical == 0 && reserved > 0 && quarantine == 0,
            StockMovementKind.Release => reservation is not null && physical == 0 && reserved < 0 && quarantine == 0,
            StockMovementKind.Consume => reservation is not null && physical < 0 && physical == reserved && quarantine == 0,
            _ => false
        };
        if (!valid) throw new ArgumentException("Los movimientos deben conservar la distinción entre físico, reservado y cuarentena.");
        Id = id; BatchId = batch; ReservationId = reservation; Kind = kind; PhysicalDelta = physical; ReservedDelta = reserved; QuarantineDelta = quarantine;
        Reason = CatalogText.Required(reason, 500, "el motivo del movimiento"); RequestHash = hash;
        ActorUserId = actor; OccurredAtUtc = utcNow;
    }
}
