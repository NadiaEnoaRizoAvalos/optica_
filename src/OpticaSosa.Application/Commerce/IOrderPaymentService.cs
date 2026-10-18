using OpticaSosa.Domain.Commerce;

namespace OpticaSosa.Application.Commerce;

public sealed record OrderPaymentInput(Guid SubmissionId, Guid OrderVersion, decimal Amount, string Currency, string Method, string? Reference = null);
public sealed record OrderRefundInput(Guid SubmissionId, Guid OrderVersion, Guid PaymentId, decimal Amount,
    string Currency, string Method, string Reason, string? Reference = null);
public sealed record OrderMoneyView(Guid Id, OrderMoneyKind Kind, long AmountMinor, string Currency, Guid? OriginalPaymentId,
    string Method, string? Reference, string? Reason, string ActorUserId, DateTime OccurredAtUtc);
public sealed record OrderMoneyPage(IReadOnlyList<OrderMoneyView> Items, int Page, bool HasMore);
public interface IOrderPaymentService
{
    Task<OrderMoneyPage?> ListAsync(Guid orderId, int page = 1, CancellationToken ct = default);
    Task<SalesResult> ReceiveAsync(Guid orderId, OrderPaymentInput input, CancellationToken ct = default);
    Task<SalesResult> RefundAsync(Guid orderId, OrderRefundInput input, CancellationToken ct = default);
}
