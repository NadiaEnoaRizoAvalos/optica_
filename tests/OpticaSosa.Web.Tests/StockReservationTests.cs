using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Commerce;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Infrastructure.Inventory;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class StockReservationTests
{
    [Fact]
    public async Task ReservationNeedsNoDepositDoesNotConsumeAndMustBeExplicitlyReleasedBeforeCancellation()
    {
        await using var test = await InventoryScenario.CreateAsync(); var batchId = await test.ReceiveAsync();
        var input = await test.ReserveInputAsync(batchId);
        Assert.True((await test.Stock.ReserveAsync(test.OrderId, test.LineId, input)).Succeeded);
        Assert.True((await test.Stock.ReserveAsync(test.OrderId, test.LineId, input)).Succeeded);
        var batch = Assert.Single((await test.Stock.BatchesAsync()).Items);
        Assert.Equal(1, batch.OnHand); Assert.Equal(1, batch.Reserved); Assert.Equal(0, batch.Available);
        var order = (await test.Sales.Orders.GetAsync(test.OrderId))!; Assert.Equal(0, order.NetPaidMinor);
        Assert.Equal(SalesResultCode.Invalid, (await test.Sales.Orders.CancelAsync(test.OrderId, new(Guid.NewGuid(), order.Version, "Cancelación ficticia"))).Code);
        var reservation = Assert.Single((await test.Stock.ReservationsAsync(test.OrderId)).Items);
        var release = new OpticaSosa.Application.Inventory.ReleaseStockInput(Guid.NewGuid(), order.Version, reservation.Version, "Liberación ficticia");
        Assert.True((await test.Stock.ReleaseAsync(test.OrderId, reservation.Id, release)).Succeeded);
        Assert.True((await test.Stock.ReleaseAsync(test.OrderId, reservation.Id, release)).Succeeded);
        batch = Assert.Single((await test.Stock.BatchesAsync()).Items); Assert.Equal(1, batch.OnHand); Assert.Equal(0, batch.Reserved);
        order = (await test.Sales.Orders.GetAsync(test.OrderId))!;
        Assert.True((await test.Sales.Orders.CancelAsync(test.OrderId, new(Guid.NewGuid(), order.Version, "Ficticio"))).Succeeded);
        var movements = (await test.Stock.MovementsAsync(batchId)).Items;
        Assert.Equal(3, movements.Count); Assert.Equal(1, movements.Sum(x => x.PhysicalDelta)); Assert.Equal(0, movements.Sum(x => x.ReservedDelta));
    }
    [Fact]
    public async Task ConcurrentOrdersCannotReserveTheLastUnitTwice()
    {
        await using var test = await InventoryScenario.CreateAsync(); var batch = await test.ReceiveAsync();
        var anotherOrder = await test.Sales.ConfirmAsync(await test.Sales.DraftAsync(test.Catalog, test.Test.CustomerId, test.VariantId));
        var anotherLine = Assert.Single((await test.Sales.Orders.LinesAsync(anotherOrder))!.Items);
        var first = await test.ReserveInputAsync(batch);
        var second = first with { SubmissionId = Guid.NewGuid(), OrderVersion = (await test.Sales.Orders.GetAsync(anotherOrder))!.Version };
        var service = new StockService(test.Sales.Factory, test.Sales.Actor, test.Clock);
        var results = await Task.WhenAll(test.Stock.ReserveAsync(test.OrderId, test.LineId, first), service.ReserveAsync(anotherOrder, anotherLine.Id, second));
        Assert.Single(results, x => x.Succeeded);
        var stock = Assert.Single((await test.Stock.BatchesAsync()).Items); Assert.Equal(1, stock.Reserved); Assert.Equal(0, stock.Available);
        await using var db = await test.Sales.Factory.CreateDbContextAsync();
        Assert.Single(await db.StockReservations.ToListAsync());
    }
    [Fact]
    public async Task QuarantineAndUnreceivedMaterialAreUnavailableAndShortagesRemainVisible()
    {
        await using var test = await InventoryScenario.CreateAsync(quantity: 3); var batch = await test.ReceiveAsync(2, quarantine: 1);
        Assert.Equal(SalesResultCode.Invalid, (await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batch, 2))).Code);
        var before = Assert.Single((await test.Stock.AvailabilityAsync(test.OrderId)).Items);
        Assert.Equal(1, before.Available); Assert.Equal(2, before.Shortage);
        Assert.True((await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batch))).Succeeded);
        var after = Assert.Single((await test.Stock.AvailabilityAsync(test.OrderId)).Items);
        Assert.Equal(1, after.ReservedOrConsumed); Assert.Equal(2, after.Unreserved); Assert.Equal(2, after.Shortage);
        Assert.Equal(1, Assert.Single((await test.Stock.BatchesAsync()).Items).Quarantined);
    }
    [Fact]
    public async Task ReservationsCannotCrossOrderLinesAndCannotExceedOrderedQuantity()
    {
        await using var test = await InventoryScenario.CreateAsync(); var batch = await test.ReceiveAsync(3);
        var input = await test.ReserveInputAsync(batch, 2);
        Assert.Equal(SalesResultCode.Invalid, (await test.Stock.ReserveAsync(test.OrderId, test.LineId, input)).Code);
        Assert.Equal(SalesResultCode.NotFound, (await test.Stock.ReserveAsync(test.OrderId, Guid.NewGuid(), input)).Code);
        Assert.True((await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batch))).Succeeded);
        Assert.Equal(SalesResultCode.Invalid, (await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batch))).Code);
        var reservation = Assert.Single((await test.Stock.ReservationsAsync(test.OrderId)).Items);
        var other = await test.Sales.ConfirmAsync(await test.Sales.DraftAsync(test.Catalog, test.Test.CustomerId, test.VariantId));
        Assert.Equal(SalesResultCode.NotFound, (await test.Stock.ReleaseAsync(other, reservation.Id,
            new(Guid.NewGuid(), (await test.Sales.Orders.GetAsync(other))!.Version, reservation.Version, "Ficticio"))).Code);
    }
}
