using OpticaSosa.Application.Commerce;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Commerce;
using OpticaSosa.Domain.Inventory;

namespace OpticaSosa.Application.Inventory;

public sealed record StockReceiptInput(Guid SubmissionId, Guid VariantId, Guid VariantVersion, int Quantity, int Quarantined,
    string? Lot, DateOnly? Expiry, ExpiryPrecision ExpiryPrecision, string Reason);
public sealed record StockBatchView(Guid Id, Guid VariantId, ProductCategory Category, string? Lot, DateOnly? ExpiresOn,
    ExpiryPrecision ExpiryPrecision, int OnHand, int Reserved, int Quarantined, int Available, Guid Version, DateTime ReceivedAtUtc);
public sealed record StockBatchPage(IReadOnlyList<StockBatchView> Items, int Page, bool HasMore);
public sealed record StockReceiptResult(bool Succeeded, Guid? BatchId = null, string? Error = null, bool Conflict = false);
public sealed record ReserveStockInput(Guid SubmissionId, Guid OrderVersion, Guid BatchId, Guid BatchVersion,
    Guid? TechnicalAssignmentId, int Quantity);
public sealed record ReleaseStockInput(Guid SubmissionId, Guid OrderVersion, Guid ReservationVersion, string Reason);
public sealed record StockReservationView(Guid Id, Guid LineId, Guid BatchId, Guid? TechnicalAssignmentId, int Quantity,
    ReservationStatus Status, Guid Version, DateTime CreatedAtUtc, DateTime? ClosedAtUtc, string? ClosureReason);
public sealed record StockReservationPage(IReadOnlyList<StockReservationView> Items, int Page, bool HasMore);
public sealed record StockMovementView(Guid Id, StockMovementKind Kind, Guid? ReservationId, int PhysicalDelta,
    int ReservedDelta, int QuarantineDelta, string Reason, string ActorUserId, DateTime OccurredAtUtc);
public sealed record StockMovementPage(IReadOnlyList<StockMovementView> Items, int Page, bool HasMore);
public sealed record OrderAvailabilityLine(Guid LineId, Guid VariantId, FulfilmentSource Source, int Ordered,
    long ReservedOrConsumed, long Unreserved, long Available, long Shortage, bool TechnicalRequired, bool TechnicalCurrent);
public sealed record OrderAvailabilityPage(IReadOnlyList<OrderAvailabilityLine> Items, int Page, bool HasMore);
public interface IStockService
{
    Task<StockBatchPage> BatchesAsync(Guid? variantId = null, int page = 1, CancellationToken ct = default);
    Task<StockReceiptResult> ReceiveAsync(StockReceiptInput input, CancellationToken ct = default);
    Task<StockReservationPage> ReservationsAsync(Guid orderId, int page = 1, CancellationToken ct = default);
    Task<StockMovementPage> MovementsAsync(Guid batchId, int page = 1, CancellationToken ct = default);
    Task<OrderAvailabilityPage> AvailabilityAsync(Guid orderId, int page = 1, CancellationToken ct = default);
    Task<SalesResult> ReserveAsync(Guid orderId, Guid lineId, ReserveStockInput input, CancellationToken ct = default);
    Task<SalesResult> ReleaseAsync(Guid orderId, Guid reservationId, ReleaseStockInput input, CancellationToken ct = default);
}
