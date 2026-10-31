using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Work;

namespace OpticaSosa.Domain.Delivery;

public sealed class CommercialWork
{
    public Guid WorkId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid? PreviousWorkId { get; private set; }
    public string? ReplacementReason { get; private set; }
    public bool IsCurrent { get; private set; } = true;
    public string? TechnicalMaterialHash { get; private set; }
    public string? DeliveryMaterialHash { get; private set; }
    private CommercialWork() { }
    public CommercialWork(Guid workId, Guid orderId, Guid? previous, string? reason)
    {
        if (workId == Guid.Empty || orderId == Guid.Empty || previous == Guid.Empty || previous == workId)
            throw new ArgumentException("Vínculo de trabajo inválido.");
        WorkId = workId; OrderId = orderId; PreviousWorkId = previous;
        ReplacementReason = previous is not null ? CatalogText.Required(reason, 500, "el motivo de reemplazo") : null;
    }
    public void Retire() => IsCurrent = false;
    public void ClearControls() => TechnicalMaterialHash = DeliveryMaterialHash = null;
    public void RecordControl(QualityStage stage, bool approved, string hash)
    {
        if (!IsCurrent || !Enum.IsDefined(stage) || hash.Length != 64) throw new ArgumentException("Control comercial inválido.");
        if (stage == QualityStage.Technical) TechnicalMaterialHash = approved ? hash : null;
        else DeliveryMaterialHash = approved ? hash : null;
    }
}

public sealed class CommercialWorkLine
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid WorkId { get; private set; }
    public Guid LineId { get; private set; }
    public Guid? TechnicalAssignmentId { get; private set; }
    public bool IsCurrent { get; private set; } = true;
    private CommercialWorkLine() { }
    public CommercialWorkLine(Guid workId, Guid lineId, Guid? assignment)
    {
        if (workId == Guid.Empty || lineId == Guid.Empty || assignment == Guid.Empty) throw new ArgumentException("Material de trabajo inválido.");
        WorkId = workId; LineId = lineId; TechnicalAssignmentId = assignment;
    }
    public void Retire() => IsCurrent = false;
}

public sealed class OrderDelivery
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public string ActorUserId { get; private set; } = "";
    public DateTime DeliveredAtUtc { get; private set; }
    private OrderDelivery() { }
    public OrderDelivery(Guid id, Guid order, string actor, DateTime now)
    {
        if (id == Guid.Empty || order == Guid.Empty || string.IsNullOrWhiteSpace(actor) || now.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Entrega inválida.");
        Id = id; OrderId = order; ActorUserId = actor; DeliveredAtUtc = now;
    }
}
