using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Commerce;

namespace OpticaSosa.Application.Commerce;

public sealed class SalesDraftInput
{
    public Guid SubmissionId { get; set; } = Guid.NewGuid();
    public Guid Version { get; set; }
    public Guid CustomerId { get; set; }
    public string Currency { get; set; } = "";
    public DateOnly? PromisedDate { get; set; }
    public IReadOnlyList<SalesLineInput> Lines { get; set; } = [];
}
public sealed record SalesLineInput(Guid VariantId, Guid VariantVersion, int Quantity, FulfilmentSource Source,
    decimal UnitPrice, decimal Discount = 0, string? DiscountReason = null);
public sealed record OrderActionInput(Guid SubmissionId, Guid Version);
public sealed record CancelOrderInput(Guid SubmissionId, Guid Version, string Reason);
public sealed record SalesOrderSummary(Guid Id, Guid CustomerId, SalesOrderStatus Status, string Currency,
    long TotalMinor, long ReceivedMinor, long RefundedMinor, DateOnly? PromisedDate, int DraftNumber, Guid Version,
    DateTime CreatedAtUtc, DateTime? ConfirmedAtUtc, DateTime? CancelledAtUtc)
{
    public long NetPaidMinor => ReceivedMinor - RefundedMinor;
    public long BalanceMinor => TotalMinor - NetPaidMinor;
}
public sealed record SalesOrderPage(IReadOnlyList<SalesOrderSummary> Items, int Page, bool HasMore);
public sealed record SalesLineView(Guid Id, Guid VariantId, string ProductName, string VariantCode, ProductCategory Category,
    ProductUnit Unit, int UnitsPerPackage, int Quantity, FulfilmentSource Source, long UnitPriceMinor, long DiscountMinor,
    string? DiscountReason, long TotalMinor, int Position);
public sealed record SalesLinePage(IReadOnlyList<SalesLineView> Items, int DraftNumber, int Page, bool HasMore);
public enum SalesResultCode { Success, Invalid, NotFound, Conflict }
public sealed record SalesResult(SalesResultCode Code, Guid? OrderId = null, string? Error = null)
{
    public bool Succeeded => Code == SalesResultCode.Success;
}
public interface ISalesOrderService
{
    Task<SalesOrderPage> ListAsync(Guid? customerId = null, int page = 1, CancellationToken ct = default);
    Task<SalesOrderSummary?> GetAsync(Guid orderId, CancellationToken ct = default);
    Task<SalesLinePage?> LinesAsync(Guid orderId, int page = 1, int? draftNumber = null, CancellationToken ct = default);
    Task<SalesResult> CreateDraftAsync(SalesDraftInput input, CancellationToken ct = default);
    Task<SalesResult> UpdateDraftAsync(Guid orderId, SalesDraftInput input, CancellationToken ct = default);
    Task<SalesResult> ConfirmAsync(Guid orderId, OrderActionInput input, CancellationToken ct = default);
    Task<SalesResult> CancelAsync(Guid orderId, CancelOrderInput input, CancellationToken ct = default);
}
