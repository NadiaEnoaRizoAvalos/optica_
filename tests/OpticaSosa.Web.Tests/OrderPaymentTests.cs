using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Commerce;
using OpticaSosa.Domain.Commerce;
using OpticaSosa.Infrastructure.Commerce;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class OrderPaymentTests
{
    [Fact]
    public async Task PaymentsAreIdempotentExactAndNeverOverpayOrChangeCurrency()
    {
        await using var test = await TechnicalTestContext.CreateAsync(); using var catalog = await CatalogScenario.OpenAsync(test.App);
        using var sales = await CommerceScenario.OpenAsync(test.App, "ventas.ficticio");
        var item = await SalesOrderTests.VariantAsync(catalog);
        var id = await sales.ConfirmAsync(await sales.DraftAsync(catalog, test.CustomerId, item.Variant));
        var order = (await sales.Orders.GetAsync(id))!;
        var input = new OrderPaymentInput(Guid.NewGuid(), order.Version, 12.345m, "ARS", "Efectivo ficticio");
        Assert.True((await sales.Payments.ReceiveAsync(id, input)).Succeeded);
        Assert.True((await sales.Payments.ReceiveAsync(id, input)).Succeeded);
        Assert.Equal(SalesResultCode.Conflict, (await sales.Payments.ReceiveAsync(id, input with { Amount = 12.35m })).Code);
        Assert.Equal(1235, (await sales.Orders.GetAsync(id))!.NetPaidMinor);
        var current = (await sales.Orders.GetAsync(id))!;
        Assert.Equal(SalesResultCode.Invalid, (await sales.Payments.ReceiveAsync(id, new(Guid.NewGuid(), current.Version, 100, "ARS", "Ficticio"))).Code);
        Assert.Equal(SalesResultCode.Invalid, (await sales.Payments.ReceiveAsync(id, new(Guid.NewGuid(), current.Version, 1, "USD", "Ficticio"))).Code);
        Assert.Equal(SalesResultCode.Invalid, (await sales.Payments.ReceiveAsync(id, new(Guid.NewGuid(), current.Version, 0, "ARS", "Ficticio"))).Code);
        await using var db = await sales.Factory.CreateDbContextAsync();
        Assert.Single(await db.OrderMoneyEntries.ToListAsync());
        Assert.Single(await db.AuditEntries.Where(x => x.Action == "sales.payment.received").ToListAsync());
    }
    [Fact]
    public async Task RefundsCompensateOriginalPaymentsAndAreRequiredBeforeAdministrativeCancellation()
    {
        await using var test = await TechnicalTestContext.CreateAsync(); using var catalog = await CatalogScenario.OpenAsync(test.App);
        using var sales = await CommerceScenario.OpenAsync(test.App, "ventas.ficticio"); using var admin = await CommerceScenario.OpenAsync(test.App);
        var item = await SalesOrderTests.VariantAsync(catalog);
        var id = await sales.ConfirmAsync(await sales.DraftAsync(catalog, test.CustomerId, item.Variant));
        var order = (await sales.Orders.GetAsync(id))!; var paymentId = Guid.NewGuid();
        Assert.True((await sales.Payments.ReceiveAsync(id, new(paymentId, order.Version, 25, "ARS", "Efectivo ficticio"))).Succeeded);
        order = (await sales.Orders.GetAsync(id))!;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sales.Payments.RefundAsync(id, new(Guid.NewGuid(), order.Version, paymentId, 25, "ARS", "Ficticio", "Ficticio")));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sales.Orders.CancelAsync(id, new(Guid.NewGuid(), order.Version, "Ficticio")));
        Assert.Equal(SalesResultCode.Invalid, (await admin.Orders.CancelAsync(id, new(Guid.NewGuid(), order.Version, "Ficticio"))).Code);
        var refund = new OrderRefundInput(Guid.NewGuid(), order.Version, paymentId, 10, "ARS", "Efectivo ficticio", "Devolución ficticia");
        Assert.True((await admin.Payments.RefundAsync(id, refund)).Succeeded);
        Assert.True((await admin.Payments.RefundAsync(id, refund)).Succeeded);
        order = (await admin.Orders.GetAsync(id))!;
        Assert.Equal(SalesResultCode.Invalid, (await admin.Payments.RefundAsync(id, refund with { SubmissionId = Guid.NewGuid(), OrderVersion = order.Version, Amount = 16 })).Code);
        Assert.True((await admin.Payments.RefundAsync(id, refund with { SubmissionId = Guid.NewGuid(), OrderVersion = order.Version, Amount = 15 })).Succeeded);
        order = (await admin.Orders.GetAsync(id))!;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sales.Orders.CancelAsync(id, new(Guid.NewGuid(), order.Version, "Ficticio")));
        var cancel = new CancelOrderInput(Guid.NewGuid(), order.Version, "Pedido cancelado ficticio");
        Assert.True((await admin.Orders.CancelAsync(id, cancel)).Succeeded);
        Assert.True((await admin.Orders.CancelAsync(id, cancel)).Succeeded);
        Assert.Equal(3, (await admin.Payments.ListAsync(id))!.Items.Count);
        var cancelled = (await admin.Orders.GetAsync(id))!;
        Assert.Equal(SalesOrderStatus.Cancelled, cancelled.Status); Assert.Equal(0, cancelled.NetPaidMinor);
        Assert.Equal(2500, cancelled.ReceivedMinor); Assert.Equal(2500, cancelled.RefundedMinor);
    }
    [Fact]
    public async Task ConcurrentPaymentsCannotSpendTheSameBalanceTwice()
    {
        await using var test = await TechnicalTestContext.CreateAsync(); using var catalog = await CatalogScenario.OpenAsync(test.App);
        using var sales = await CommerceScenario.OpenAsync(test.App);
        var item = await SalesOrderTests.VariantAsync(catalog);
        var id = await sales.ConfirmAsync(await sales.DraftAsync(catalog, test.CustomerId, item.Variant));
        var version = (await sales.Orders.GetAsync(id))!.Version;
        var second = new OrderPaymentService(sales.Factory, sales.Actor, TimeProvider.System);
        var results = await Task.WhenAll(sales.Payments.ReceiveAsync(id, new(Guid.NewGuid(), version, 75, "ARS", "Ficticio")),
            second.ReceiveAsync(id, new(Guid.NewGuid(), version, 75, "ARS", "Ficticio")));
        Assert.Single(results, x => x.Succeeded);
        Assert.Equal(7500, (await sales.Orders.GetAsync(id))!.NetPaidMinor);
        Assert.Single((await sales.Payments.ListAsync(id))!.Items);
    }
    [Fact]
    public async Task PaymentAndAuditRollBackTogetherWhenTheAuditCannotBeWritten()
    {
        await using var test = await TechnicalTestContext.CreateAsync(); using var catalog = await CatalogScenario.OpenAsync(test.App);
        using var sales = await CommerceScenario.OpenAsync(test.App);
        var item = await SalesOrderTests.VariantAsync(catalog);
        var id = await sales.ConfirmAsync(await sales.DraftAsync(catalog, test.CustomerId, item.Variant));
        var version = (await sales.Orders.GetAsync(id))!.Version;
        await using var db = await sales.Factory.CreateDbContextAsync();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectFictitiousPaymentAudit BEFORE INSERT ON AuditEntries WHEN NEW.Action = 'sales.payment.received' BEGIN SELECT RAISE(ABORT, 'Fictitious audit failure'); END;");
        var result = await sales.Payments.ReceiveAsync(id, new(Guid.NewGuid(), version, 10, "ARS", "Ficticio"));
        Assert.False(result.Succeeded);
        Assert.Equal(0, (await sales.Orders.GetAsync(id))!.NetPaidMinor);
        Assert.Empty((await sales.Payments.ListAsync(id))!.Items);
        Assert.Equal(2, await db.OrderCommandReceipts.CountAsync());
    }
    [Fact]
    public async Task RefundCannotReferenceAnotherOrderOrAnotherRefund()
    {
        await using var test = await TechnicalTestContext.CreateAsync(); using var catalog = await CatalogScenario.OpenAsync(test.App);
        using var admin = await CommerceScenario.OpenAsync(test.App);
        var item = await SalesOrderTests.VariantAsync(catalog);
        var first = await admin.ConfirmAsync(await admin.DraftAsync(catalog, test.CustomerId, item.Variant));
        var second = await admin.ConfirmAsync(await admin.DraftAsync(catalog, test.CustomerId, item.Variant));
        var firstVersion = (await admin.Orders.GetAsync(first))!.Version; var payment = Guid.NewGuid();
        Assert.True((await admin.Payments.ReceiveAsync(first, new(payment, firstVersion, 25, "ARS", "Ficticio"))).Succeeded);
        var secondVersion = (await admin.Orders.GetAsync(second))!.Version;
        Assert.Equal(SalesResultCode.Invalid, (await admin.Payments.RefundAsync(second, new(Guid.NewGuid(), secondVersion, payment, 10, "ARS", "Ficticio", "Ficticio"))).Code);
        firstVersion = (await admin.Orders.GetAsync(first))!.Version; var refund = Guid.NewGuid();
        Assert.True((await admin.Payments.RefundAsync(first, new(refund, firstVersion, payment, 10, "ARS", "Ficticio", "Ficticio"))).Succeeded);
        firstVersion = (await admin.Orders.GetAsync(first))!.Version;
        Assert.Equal(SalesResultCode.Invalid, (await admin.Payments.RefundAsync(first, new(Guid.NewGuid(), firstVersion, refund, 1, "ARS", "Ficticio", "Ficticio"))).Code);
    }
    [Fact]
    public async Task SqliteRejectsNegativeAmountsAndCurrencyMismatch()
    {
        await using var test = await TechnicalTestContext.CreateAsync(); using var catalog = await CatalogScenario.OpenAsync(test.App);
        using var admin = await CommerceScenario.OpenAsync(test.App);
        var item = await SalesOrderTests.VariantAsync(catalog);
        var id = await admin.ConfirmAsync(await admin.DraftAsync(catalog, test.CustomerId, item.Variant));
        var paymentId = Guid.NewGuid(); var version = (await admin.Orders.GetAsync(id))!.Version;
        Assert.True((await admin.Payments.ReceiveAsync(id, new(paymentId, version, 10, "ARS", "Ficticio"))).Succeeded);
        await using var db = await admin.Factory.CreateDbContextAsync();
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE SalesOrders SET ReceivedMinor = -1 WHERE Id = {id}"));
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE SalesLines SET Quantity = 0 WHERE OrderId = {id}"));
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE OrderMoneyEntries SET Currency = 'USD' WHERE Id = {paymentId}"));
        Assert.Equal(1000, (await admin.Orders.GetAsync(id))!.NetPaidMinor);
    }
}
