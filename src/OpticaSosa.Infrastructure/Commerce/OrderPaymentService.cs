using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Commerce;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Commerce;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Commerce;

public sealed class OrderPaymentService(IDbContextFactory<OpticaDbContext> factory, ICurrentUser current, TimeProvider clock) : IOrderPaymentService
{
    public async Task<OrderMoneyPage?> ListAsync(Guid orderId, int page = 1, CancellationToken ct = default)
    {
        SalesOperations.Demand(current, Permissions.ReadSales);
        var skip = SalesOperations.Skip(page);
        await using var db = await factory.CreateDbContextAsync(ct);
        if (!await db.SalesOrders.AnyAsync(x => x.Id == orderId, ct)) return null;
        var rows = await db.OrderMoneyEntries.AsNoTracking().Where(x => x.OrderId == orderId)
            .OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.Id).Skip(skip).Take(51).ToListAsync(ct);
        return new(rows.Take(50).Select(x => new OrderMoneyView(x.Id, x.Kind, x.AmountMinor, x.Currency, x.OriginalPaymentId,
            x.Method, x.Reference, x.Reason, x.ActorUserId, x.OccurredAtUtc)).ToArray(), page, rows.Count > 50);
    }
    public Task<SalesResult> ReceiveAsync(Guid orderId, OrderPaymentInput input, CancellationToken ct = default)
    {
        var actor = SalesOperations.Demand(current, Permissions.ReceivePayments);
        return SalesOperations.ExecuteAsync(factory, clock, actor, orderId, input.SubmissionId, "sales.payment.received", input, async db =>
        {
            var order = await db.SalesOrders.SingleOrDefaultAsync(x => x.Id == orderId, ct);
            if (order is null) return SalesOperations.Missing();
            if (order.Version != input.OrderVersion) return SalesOperations.Conflict();
            var amount = Money.FromDecimal(input.Amount, input.Currency);
            if (amount.Currency != order.Currency) return SalesOperations.Invalid("El pago debe usar la moneda del pedido.");
            var entry = new OrderMoneyEntry(input.SubmissionId, orderId, OrderMoneyKind.Payment, amount, null,
                input.Method, input.Reference, null, actor, clock.GetUtcNow().UtcDateTime);
            order.Receive(amount.MinorUnits); db.OrderMoneyEntries.Add(entry);
            return null;
        }, ct);
    }
    public Task<SalesResult> RefundAsync(Guid orderId, OrderRefundInput input, CancellationToken ct = default)
    {
        var actor = SalesOperations.Demand(current, Permissions.RefundPayments);
        return SalesOperations.ExecuteAsync(factory, clock, actor, orderId, input.SubmissionId, "sales.payment.refunded", input, async db =>
        {
            var order = await db.SalesOrders.SingleOrDefaultAsync(x => x.Id == orderId, ct);
            if (order is null) return SalesOperations.Missing();
            if (order.Version != input.OrderVersion) return SalesOperations.Conflict();
            var original = await db.OrderMoneyEntries.SingleOrDefaultAsync(x => x.Id == input.PaymentId && x.OrderId == orderId && x.Kind == OrderMoneyKind.Payment, ct);
            if (original is null) return SalesOperations.Invalid("Seleccioná un pago original de este pedido.");
            var amount = Money.FromDecimal(input.Amount, input.Currency);
            if (amount.Currency != order.Currency) return SalesOperations.Invalid("La devolución debe usar la moneda del pedido.");
            var refunded = await db.OrderMoneyEntries.Where(x => x.OriginalPaymentId == original.Id).SumAsync(x => x.AmountMinor, ct);
            if (amount.MinorUnits > original.AmountMinor - refunded) return SalesOperations.Invalid("La devolución supera el remanente del pago original.");
            var entry = new OrderMoneyEntry(input.SubmissionId, orderId, OrderMoneyKind.Refund, amount, original.Id,
                input.Method, input.Reference, input.Reason, actor, clock.GetUtcNow().UtcDateTime);
            order.Refund(amount.MinorUnits); db.OrderMoneyEntries.Add(entry);
            return null;
        }, ct);
    }
}
