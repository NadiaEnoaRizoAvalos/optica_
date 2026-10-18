using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Technical;

namespace OpticaSosa.Domain.Inventory;

[Flags]
public enum TechnicalEyes { None = 0, Right = 1, Left = 2, Both = Right | Left }

public sealed class StockReservation
{
    public Guid Id { get; private set; }
    public Guid LineId { get; private set; }
    public Guid BatchId { get; private set; }
    public Guid? TechnicalAssignmentId { get; private set; }
    public int Quantity { get; private set; }
    public ReservationStatus Status { get; private set; } = ReservationStatus.Active;
    public string CreatedByUserId { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }
    public string? ClosedByUserId { get; private set; }
    public string? ClosureReason { get; private set; }
    public Guid Version { get; private set; }
    private StockReservation() { }
    public StockReservation(Guid id, Guid line, Guid batch, Guid? assignment, int quantity, string actor, DateTime utcNow)
    {
        if (id == Guid.Empty || line == Guid.Empty || batch == Guid.Empty || assignment == Guid.Empty || quantity <= 0 || string.IsNullOrWhiteSpace(actor) || utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Reserva inválida.");
        Id = id; LineId = line; BatchId = batch; TechnicalAssignmentId = assignment; Quantity = quantity;
        CreatedByUserId = actor; CreatedAtUtc = utcNow; Version = Guid.NewGuid();
    }
    public void Close(ReservationStatus status, string actor, DateTime utcNow, string reason)
    {
        if (Status != ReservationStatus.Active || status is not (ReservationStatus.Released or ReservationStatus.Consumed) ||
            string.IsNullOrWhiteSpace(actor) || utcNow.Kind != DateTimeKind.Utc) throw new ArgumentException("La reserva ya se cerró o el estado no es válido.");
        ClosureReason = CatalogText.Required(reason, 500, "el motivo"); Status = status;
        ClosedAtUtc = utcNow; ClosedByUserId = actor; Version = Guid.NewGuid();
    }
}

public sealed class OrderTechnicalAssignment
{
    public Guid Id { get; private set; }
    public Guid LineId { get; private set; }
    public Guid PublicationId { get; private set; }
    public TechnicalEyes Eyes { get; private set; }
    public bool IsCurrent { get; private set; } = true;
    public Guid? PreviousId { get; private set; }
    public string? ReplacementReason { get; private set; }
    public string AssignedByUserId { get; private set; } = "";
    public DateTime AssignedAtUtc { get; private set; }
    public Guid Version { get; private set; }
    private OrderTechnicalAssignment() { }
    public OrderTechnicalAssignment(Guid id, Guid line, Guid publication, TechnicalEyes eyes, Guid? previousId,
        string? reason, string actor, DateTime utcNow)
    {
        if (id == Guid.Empty || line == Guid.Empty || publication == Guid.Empty || previousId == Guid.Empty || previousId == id ||
            eyes is not (TechnicalEyes.Right or TechnicalEyes.Left or TechnicalEyes.Both) || string.IsNullOrWhiteSpace(actor) || utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Asignación técnica inválida.");
        Id = id; LineId = line; PublicationId = publication; Eyes = eyes; PreviousId = previousId;
        ReplacementReason = previousId is not null ? CatalogText.Required(reason, 500, "el motivo de nueva vinculación") : null;
        AssignedByUserId = actor; AssignedAtUtc = utcNow; Version = Guid.NewGuid();
    }
    public void Supersede() { IsCurrent = false; Version = Guid.NewGuid(); }
}
