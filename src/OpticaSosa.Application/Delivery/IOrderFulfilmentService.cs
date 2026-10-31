using OpticaSosa.Application.Commerce;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Work;

namespace OpticaSosa.Application.Delivery;

public sealed record WorkMaterialInput(Guid LineId, Guid? TechnicalAssignmentId = null);
public sealed record CommercialWorkInput(Guid SubmissionId, Guid OrderVersion, IReadOnlyList<WorkMaterialInput> Materials,
    string FrameReference = "", Guid? PreviousWorkId = null, Guid? PreviousWorkVersion = null, string? ReplacementReason = null);
public sealed record ReopenWorkInput(Guid SubmissionId, Guid OrderVersion, Guid WorkVersion, string Reason);
public sealed record DeliveryWorkVersion(Guid WorkId, Guid Version);
public sealed record DeliverOrderInput(Guid SubmissionId, Guid OrderVersion, IReadOnlyList<DeliveryWorkVersion> Works);
public enum FulfilmentState { NotGrouped, WaitingMaterials, Preparing, UnderReview, Ready, Delivered, Cancelled }
public sealed record FulfilmentMaterial(Guid LineId, Guid VariantId, string Product, string Code, ProductCategory Category,
    int Quantity, Guid? AssignmentId, Guid? WorkId, long Reserved);
public sealed record FulfilmentWork(Guid WorkId, Guid Version, OpticalWorkType Type, WorkStatus Status, int Round,
    bool IsCurrent, bool MaterialsReady, bool ControlsMatchMaterials, string? BlockReason,
    Guid? PreviousWorkId = null, string? ReplacementReason = null);
public sealed record FulfilmentPage<T>(IReadOnlyList<T> Items, int Page, bool HasMore);
public sealed record FulfilmentSummary(Guid OrderId, Guid CustomerId, Guid OrderVersion, FulfilmentState State,
    bool BalanceSettled, string? DeliveredBy, DateTime? DeliveredAtUtc);

public interface IOrderFulfilmentService
{
    Task<FulfilmentSummary?> GetAsync(Guid orderId, CancellationToken ct = default);
    Task<FulfilmentPage<FulfilmentMaterial>> MaterialsAsync(Guid orderId, int page = 1, CancellationToken ct = default);
    Task<FulfilmentPage<FulfilmentWork>> WorksAsync(Guid orderId, bool includeHistory = false, int page = 1, CancellationToken ct = default);
    Task<SalesResult> CreateWorkAsync(Guid orderId, CommercialWorkInput input, CancellationToken ct = default);
    Task<SalesResult> ReopenAsync(Guid orderId, Guid workId, ReopenWorkInput input, CancellationToken ct = default);
    Task<SalesResult> DeliverAsync(Guid orderId, DeliverOrderInput input, CancellationToken ct = default);
}
