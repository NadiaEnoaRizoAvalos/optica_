using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Commerce;
using Xunit;

namespace OpticaSosa.Infrastructure.Tests;

public sealed class SalesDomainTests
{
    private static SalesOrder Order() => new(Guid.NewGuid(), Guid.NewGuid(), "ARS", "actor-ficticio", DateTime.UtcNow);
    [Fact]
    public void ConfirmedPricesAreImmutableAndPaymentsCannotExceedTheBalance()
    {
        var order = Order(); order.Revise(1000, new(2026, 11, 1));
        Assert.Throws<ArgumentException>(() => order.Receive(100));
        order.Confirm("actor-ficticio", DateTime.UtcNow, 1); order.Receive(400);
        Assert.Equal(600, order.BalanceMinor);
        Assert.Throws<ArgumentException>(() => order.Receive(601));
        Assert.Throws<ArgumentException>(() => order.Revise(2000, null));
        Assert.Throws<ArgumentException>(() => order.Cancel("actor-ficticio", DateTime.UtcNow, "Ficticio"));
        order.Refund(400); order.Cancel("actor-ficticio", DateTime.UtcNow, "Ficticio");
        Assert.Equal(400, order.ReceivedMinor); Assert.Equal(400, order.RefundedMinor);
        Assert.Throws<ArgumentException>(() => order.Receive(100));
    }
    [Fact]
    public void DiscountsAreLineAmountsWithMandatoryReasonsAndCheckedTotals()
    {
        var variant = new ProductVariant(Guid.NewGuid(), Guid.NewGuid(), new("FICTICIO", ProductCategory.Accessory), null, "actor-ficticio", DateTime.UtcNow);
        SalesLine Line(int count, long price, long discount, string? reason) => new(Guid.NewGuid(), Guid.NewGuid(), 1, 1, variant, "Accesorio ficticio",
            count, FulfilmentSource.Stock, new(price, "ARS"), new(discount, "ARS"), reason);
        Assert.Equal(175, Line(2, 100, 25, "Motivo ficticio").TotalMinor);
        Assert.Throws<ArgumentException>(() => Line(2, 100, 201, "Ficticio"));
        Assert.Throws<ArgumentException>(() => Line(2, 100, 1, null));
        Assert.Throws<ArgumentException>(() => Line(0, 100, 0, null));
        Assert.Throws<ArgumentException>(() => Line(2, long.MaxValue, 0, null));
        Assert.Equal(1235, Money.FromDecimal(12.345m, "ARS").MinorUnits);
    }
}
