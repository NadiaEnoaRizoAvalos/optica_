using OpticaSosa.Domain.Catalog;

namespace OpticaSosa.Domain.Laboratory;

public enum LaboratoryStatus { Authorized = 1, Sent = 2, PartiallyReceived = 3, Received = 4, Cancelled = 5, Closed = 6 }
public enum LaboratoryChangeKind { Reschedule = 1, Cancel = 2 }

public sealed class LaboratoryOrder
{
    public Guid Id { get; private set; }
    public Guid LineId { get; private set; }
    public Guid SupplierId { get; private set; }
    public Guid? TechnicalAssignmentId { get; private set; }
    public int Quantity { get; private set; }
    public int ReceivedQuantity { get; private set; }
    public int QuarantinedQuantity { get; private set; }
    public int CancelledQuantity { get; private set; }
    public DateOnly? EstimatedDate { get; private set; }
    public string AuthorizedByUserId { get; private set; } = "";
    public DateTime AuthorizedAtUtc { get; private set; }
    public string? SentByUserId { get; private set; }
    public DateTime? SentAtUtc { get; private set; }
    public string? DispatchReference { get; private set; }
    public DateTime? LastReceivedAtUtc { get; private set; }
    public Guid Version { get; private set; }
    public int PendingQuantity => Quantity - ReceivedQuantity - CancelledQuantity;
    public int UsableReceivedQuantity => ReceivedQuantity - QuarantinedQuantity;
    public LaboratoryStatus Status => PendingQuantity == 0
        ? ReceivedQuantity == 0 ? LaboratoryStatus.Cancelled : CancelledQuantity == 0 ? LaboratoryStatus.Received : LaboratoryStatus.Closed
        : SentAtUtc is null ? LaboratoryStatus.Authorized : ReceivedQuantity == 0 ? LaboratoryStatus.Sent : LaboratoryStatus.PartiallyReceived;
    private LaboratoryOrder() { }
    public LaboratoryOrder(Guid id, Guid line, Guid supplier, Guid? assignment, int quantity, DateOnly? estimatedDate, string actor, DateTime utcNow)
    {
        ValidateActor(actor, utcNow);
        if (id == Guid.Empty || line == Guid.Empty || supplier == Guid.Empty || assignment == Guid.Empty || quantity <= 0)
            throw new ArgumentException("Revisá identificación y cantidad del encargo.");
        Id = id; LineId = line; SupplierId = supplier; TechnicalAssignmentId = assignment; Quantity = quantity; EstimatedDate = estimatedDate;
        AuthorizedByUserId = actor; AuthorizedAtUtc = utcNow; Touch();
    }
    public void RecordDispatch(string actor, DateTime utcNow, string reference)
    {
        ValidateActor(actor, utcNow);
        if (SentAtUtc is not null || PendingQuantity <= 0) throw new ArgumentException("El encargo ya se envió o no tiene cantidades pendientes.");
        DispatchReference = CatalogText.Required(reference, 160, "la referencia del envío"); SentByUserId = actor; SentAtUtc = utcNow; Touch();
    }
    public void Receive(int quantity, int quarantined, DateTime utcNow)
    {
        if (SentAtUtc is null || utcNow.Kind != DateTimeKind.Utc || quantity <= 0 || quantity > PendingQuantity || quarantined < 0 || quarantined > quantity)
            throw new ArgumentException("La recepción requiere envío registrado y no puede superar lo pendiente.");
        ReceivedQuantity = checked(ReceivedQuantity + quantity); QuarantinedQuantity = checked(QuarantinedQuantity + quarantined);
        LastReceivedAtUtc = utcNow; Touch();
    }
    public void Cancel(int quantity, string? laboratoryConfirmation)
    {
        if (quantity <= 0 || quantity > PendingQuantity) throw new ArgumentException("Sólo se cancela material todavía no recibido.");
        if (SentAtUtc is not null) CatalogText.Required(laboratoryConfirmation, 500, "la confirmación de cancelación del laboratorio");
        CancelledQuantity = checked(CancelledQuantity + quantity); Touch();
    }
    public void Reschedule(DateOnly? estimatedDate)
    {
        if (PendingQuantity <= 0) throw new ArgumentException("No se puede reprogramar un encargo cerrado.");
        EstimatedDate = estimatedDate; Touch();
    }
    private void Touch() => Version = Guid.NewGuid();
    internal static void ValidateActor(string actor, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(actor) || utcNow.Kind != DateTimeKind.Utc) throw new ArgumentException("Responsable o fecha inválidos.");
    }
}
