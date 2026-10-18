using OpticaSosa.Application.Commerce;
using OpticaSosa.Application.Inventory;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Laboratory;

namespace OpticaSosa.Application.Laboratory;

public sealed record LaboratoryCreateInput(Guid SubmissionId, Guid OrderVersion, Guid SupplierId, Guid SupplierVersion,
    Guid? TechnicalAssignmentId, int Quantity, DateOnly? EstimatedDate, bool CommercialConfirmed);
public sealed record LaboratoryDispatchInput(Guid SubmissionId, Guid OrderVersion, Guid LaboratoryVersion, string Reference);
public sealed record LaboratoryReceiveInput(Guid SubmissionId, Guid OrderVersion, Guid LaboratoryVersion, int Quantity,
    int Quarantined, string? Lot, DateOnly? Expiry, ExpiryPrecision ExpiryPrecision, string Reason, string? Reference = null, bool? QuarantinedContactPackageOpened = null);
public sealed record LaboratoryCancelInput(Guid SubmissionId, Guid OrderVersion, Guid LaboratoryVersion, int Quantity,
    string Reason, string? LaboratoryConfirmation = null);
public sealed record LaboratoryRescheduleInput(Guid SubmissionId, Guid OrderVersion, Guid LaboratoryVersion, DateOnly? EstimatedDate, string Reason);
public sealed record LaboratoryDetails(Guid Id, Guid OrderId, Guid OrderVersion, Guid CustomerId, Guid LineId, Guid SupplierId,
    string LaboratoryName, Guid? TechnicalAssignmentId, int Quantity, int Received, int Quarantined, int Cancelled, int Pending,
    LaboratoryStatus Status, bool TechnicalCurrent, DateOnly? EstimatedDate, bool Delayed, string AuthorizedByUserId,
    DateTime AuthorizedAtUtc, DateTime? SentAtUtc, string? DispatchReference, DateTime? LastReceivedAtUtc, Guid Version);
public sealed record LaboratoryPage(IReadOnlyList<LaboratoryDetails> Items, int Page, bool HasMore);
public sealed record LaboratoryReceiptView(Guid Id, Guid BatchId, int Quantity, int QuarantinedQuantity, string? Reference,
    string Reason, string ActorUserId, DateTime ReceivedAtUtc);
public sealed record LaboratoryReceiptPage(IReadOnlyList<LaboratoryReceiptView> Items, int Page, bool HasMore);
public sealed record LaboratoryChangeView(Guid Id, LaboratoryChangeKind Kind, int CancelledQuantity, DateOnly? PreviousEstimatedDate,
    DateOnly? NewEstimatedDate, string Reason, string? LaboratoryConfirmation, string ActorUserId, DateTime OccurredAtUtc);
public sealed record LaboratoryChangePage(IReadOnlyList<LaboratoryChangeView> Items, int Page, bool HasMore);
public sealed record LaboratoryDocument(Guid LaboratoryOrderId, string LaboratoryName, Guid OrderReference, Guid LineReference,
    string Product, string VariantCode, ProductUnit Unit, int UnitsPerPackage, int OrderedQuantity, int CancelledQuantity, DateOnly? EstimatedDate,
    OrderTechnicalProjection? Technical, DateTime GeneratedAtUtc);
public sealed record LaboratoryDocumentResult(LaboratoryDocument? Document, string? Error = null)
{
    public bool Succeeded => Document is not null;
}
public interface ILaboratoryService
{
    Task<LaboratoryPage> ListAsync(Guid? orderId = null, int page = 1, CancellationToken ct = default);
    Task<LaboratoryDetails?> GetAsync(Guid orderId, Guid laboratoryOrderId, CancellationToken ct = default);
    Task<LaboratoryReceiptPage?> ReceiptsAsync(Guid orderId, Guid laboratoryOrderId, int page = 1, CancellationToken ct = default);
    Task<LaboratoryChangePage?> ChangesAsync(Guid orderId, Guid laboratoryOrderId, int page = 1, CancellationToken ct = default);
    Task<SalesResult> CreateAsync(Guid orderId, Guid lineId, LaboratoryCreateInput input, CancellationToken ct = default);
    Task<SalesResult> RecordDispatchAsync(Guid orderId, Guid laboratoryOrderId, LaboratoryDispatchInput input, CancellationToken ct = default);
    Task<SalesResult> ReceiveAsync(Guid orderId, Guid laboratoryOrderId, LaboratoryReceiveInput input, CancellationToken ct = default);
    Task<SalesResult> CancelAsync(Guid orderId, Guid laboratoryOrderId, LaboratoryCancelInput input, CancellationToken ct = default);
    Task<SalesResult> RescheduleAsync(Guid orderId, Guid laboratoryOrderId, LaboratoryRescheduleInput input, CancellationToken ct = default);
    Task<LaboratoryDocumentResult> DocumentAsync(Guid orderId, Guid laboratoryOrderId, CancellationToken ct = default);
}
