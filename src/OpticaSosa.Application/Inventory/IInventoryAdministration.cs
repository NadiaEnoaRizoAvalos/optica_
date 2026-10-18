using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Inventory;

namespace OpticaSosa.Application.Inventory;

public sealed record InventoryResult(bool Succeeded, Guid? ReferenceId = null, string? Error = null, bool Conflict = false);
public sealed record InventoryAdjustmentInput(Guid SubmissionId, Guid BatchVersion, int Delta, bool Quarantined,
    string Reason, Guid? HoldId = null, Guid? HoldVersion = null);
public sealed record InventoryQuarantineInput(Guid SubmissionId, Guid BatchVersion, int Quantity, string Reason, bool? ContactPackageOpened = null);
public sealed record InventoryWithdrawalInput(Guid SubmissionId, Guid BatchVersion, int Quantity, string Reason,
    Guid? HoldId = null, Guid? HoldVersion = null, Guid? SupplierId = null);
public sealed record InventoryReturnInput(Guid SubmissionId, Guid BatchVersion, Guid ReservationVersion, int Quantity, string Reason, bool? ContactPackageOpened = null);
public sealed record InventoryReviewInput(Guid SubmissionId, Guid BatchVersion, Guid HoldVersion, int Quantity, bool Approve,
    bool ProductMatches, bool ItemIntact, bool? SealIntact, string Reason);
public sealed record StockHoldView(Guid Id, Guid BatchId, ProductCategory Category, StockHoldOrigin Origin, int Quantity,
    int Released, int Removed, int Pending, bool OpenedContact, string Reason, string CreatedByUserId, DateTime CreatedAtUtc, Guid Version);
public sealed record StockHoldPage(IReadOnlyList<StockHoldView> Items, int Page, bool HasMore);
public sealed record StockReviewView(Guid Id, Guid HoldId, int Quantity, bool Approved, bool ProductMatches, bool ItemIntact,
    bool? SealIntact, string Reason, string ActorUserId, StockReviewerRole Role, DateTime ReviewedAtUtc);
public sealed record StockReviewPage(IReadOnlyList<StockReviewView> Items, int Page, bool HasMore);
public sealed record StockReturnView(Guid Id, Guid ReservationId, Guid HoldId, int Quantity, bool? ContactPackageOpened,
    string Reason, string ActorUserId, DateTime ReturnedAtUtc);
public sealed record StockReturnPage(IReadOnlyList<StockReturnView> Items, int Page, bool HasMore);
public sealed record StockReconciliation(Guid BatchId, int OnHand, int Reserved, int Quarantined, long LedgerPhysical,
    long LedgerReserved, long LedgerQuarantined, long OpenHolds)
{
    public bool Consistent => OnHand == LedgerPhysical && Reserved == LedgerReserved && Quarantined == LedgerQuarantined && Quarantined == OpenHolds;
}
public interface IInventoryAdministration
{
    Task<StockHoldPage> HoldsAsync(Guid? batchId = null, bool includeClosed = false, int page = 1, CancellationToken ct = default);
    Task<StockReviewPage> ReviewsAsync(Guid holdId, int page = 1, CancellationToken ct = default);
    Task<StockReturnPage> ReturnsAsync(Guid reservationId, int page = 1, CancellationToken ct = default);
    Task<StockReconciliation?> ReconciliationAsync(Guid batchId, CancellationToken ct = default);
    Task<InventoryResult> AdjustAsync(Guid batchId, InventoryAdjustmentInput input, CancellationToken ct = default);
    Task<InventoryResult> QuarantineAsync(Guid batchId, InventoryQuarantineInput input, CancellationToken ct = default);
    Task<InventoryResult> WriteOffAsync(Guid batchId, InventoryWithdrawalInput input, CancellationToken ct = default);
    Task<InventoryResult> ReturnToSupplierAsync(Guid batchId, InventoryWithdrawalInput input, CancellationToken ct = default);
    Task<InventoryResult> ReviewAsync(Guid batchId, Guid holdId, InventoryReviewInput input, CancellationToken ct = default);
    Task<InventoryResult> ReturnFromCustomerAsync(Guid reservationId, InventoryReturnInput input, CancellationToken ct = default);
}
