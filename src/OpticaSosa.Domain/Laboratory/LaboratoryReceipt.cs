using OpticaSosa.Domain.Catalog;

namespace OpticaSosa.Domain.Laboratory;

public sealed class LaboratoryReceipt
{
    public Guid Id { get; private set; }
    public Guid LaboratoryOrderId { get; private set; }
    public Guid BatchId { get; private set; }
    public int Quantity { get; private set; }
    public int QuarantinedQuantity { get; private set; }
    public string? Reference { get; private set; }
    public string Reason { get; private set; } = "";
    public string ActorUserId { get; private set; } = "";
    public DateTime ReceivedAtUtc { get; private set; }
    private LaboratoryReceipt() { }
    public LaboratoryReceipt(Guid id, Guid laboratoryOrder, Guid batch, int quantity, int quarantined, string? reference, string reason, string actor, DateTime utcNow)
    {
        LaboratoryOrder.ValidateActor(actor, utcNow);
        if (id == Guid.Empty || laboratoryOrder == Guid.Empty || batch == Guid.Empty || quantity <= 0 || quarantined < 0 || quarantined > quantity)
            throw new ArgumentException("Recepción inválida.");
        Id = id; LaboratoryOrderId = laboratoryOrder; BatchId = batch; Quantity = quantity; QuarantinedQuantity = quarantined;
        Reference = CatalogText.Optional(reference, 160); Reason = CatalogText.Required(reason, 500, "el resultado de recepción");
        ActorUserId = actor; ReceivedAtUtc = utcNow;
    }
}

public sealed class LaboratoryChange
{
    public Guid Id { get; private set; }
    public Guid LaboratoryOrderId { get; private set; }
    public LaboratoryChangeKind Kind { get; private set; }
    public int CancelledQuantity { get; private set; }
    public DateOnly? PreviousEstimatedDate { get; private set; }
    public DateOnly? NewEstimatedDate { get; private set; }
    public string Reason { get; private set; } = "";
    public string? LaboratoryConfirmation { get; private set; }
    public string ActorUserId { get; private set; } = "";
    public DateTime OccurredAtUtc { get; private set; }
    private LaboratoryChange() { }
    public LaboratoryChange(Guid id, Guid laboratoryOrder, LaboratoryChangeKind kind, int quantity, DateOnly? previousDate,
        DateOnly? newDate, string reason, string? confirmation, string actor, DateTime utcNow)
    {
        LaboratoryOrder.ValidateActor(actor, utcNow);
        if (id == Guid.Empty || laboratoryOrder == Guid.Empty || !Enum.IsDefined(kind) ||
            (kind == LaboratoryChangeKind.Cancel ? quantity <= 0 || previousDate is not null || newDate is not null : quantity != 0))
            throw new ArgumentException("Cambio de encargo inválido.");
        Id = id; LaboratoryOrderId = laboratoryOrder; Kind = kind; CancelledQuantity = quantity;
        PreviousEstimatedDate = previousDate; NewEstimatedDate = newDate;
        Reason = CatalogText.Required(reason, 500, "el motivo"); LaboratoryConfirmation = CatalogText.Optional(confirmation, 500);
        ActorUserId = actor; OccurredAtUtc = utcNow;
    }
}
