using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Delivery;
using OpticaSosa.Application.Inventory;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Delivery;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Domain.Work;
using OpticaSosa.Infrastructure.Delivery;
using OpticaSosa.Infrastructure.Inventory;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class OrderDeliveryTests
{
    [Theory]
    [InlineData(ProductCategory.Frame)]
    [InlineData(ProductCategory.Lens)]
    [InlineData(ProductCategory.ContactLens)]
    [InlineData(ProductCategory.Accessory)]
    public async Task CompleteDeliveryUsesBothChecksExistingReservationsAndExactlyOnePhysicalExit(ProductCategory category)
    {
        await using var test = await InventoryScenario.CreateAsync(category);
        var delivery = new DeliveryScenario(test);
        var assignment = category is ProductCategory.Lens or ProductCategory.ContactLens ? await test.AssignAsync() : (Guid?)null;
        var batch = await test.ReceiveAsync(lot: category == ProductCategory.ContactLens ? "LOTE-FIC" : null,
            expiry: category == ProductCategory.ContactLens ? new(2026, 11, 30) : null,
            precision: category == ProductCategory.ContactLens ? ExpiryPrecision.Day : ExpiryPrecision.None);
        Assert.True((await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batch, assignment: assignment))).Succeeded);
        var work = await delivery.GroupAsync(assignment); await delivery.PayAsync(); await delivery.ApproveAsync(work);
        Assert.Equal(FulfilmentState.Ready, (await delivery.Fulfilment.GetAsync(test.OrderId))!.State);
        Assert.False((await delivery.SalesWork.TransitionAsync(work, (await delivery.SalesWork.GetAsync(work))!.Work.Version, "deliver")).Succeeded);
        var input = await delivery.DeliveryAsync();
        var result = await delivery.Fulfilment.DeliverAsync(test.OrderId, input); Assert.True(result.Succeeded, result.Error);
        Assert.True((await delivery.Fulfilment.DeliverAsync(test.OrderId, input)).Succeeded);
        Assert.False((await delivery.Fulfilment.DeliverAsync(test.OrderId, await delivery.DeliveryAsync())).Succeeded);
        var summary = (await delivery.Fulfilment.GetAsync(test.OrderId))!;
        Assert.Equal(FulfilmentState.Delivered, summary.State); Assert.NotNull(summary.DeliveredBy); Assert.NotNull(summary.DeliveredAtUtc);
        Assert.Equal(0, (await test.Stock.BatchAsync(batch))!.OnHand);
        Assert.Equal(ReservationStatus.Consumed, Assert.Single((await test.Stock.ReservationsAsync(test.OrderId)).Items).Status);
        await using var db = await test.Sales.Factory.CreateDbContextAsync();
        Assert.Single(await db.Set<OrderDelivery>().ToListAsync()); Assert.Single(await db.StockDeliveryProofs.ToListAsync());
        Assert.Single(await db.StockMovements.Where(x => x.Kind == StockMovementKind.Consume).ToListAsync());
        Assert.Single(await db.AuditEntries.Where(x => x.Action == "delivery.order.completed").ToListAsync());
        Assert.Equal(2, await db.QualityControls.CountAsync(x => x.WorkId == work));
        Assert.True((await new InventoryAdministration(test.Sales.Factory, test.Sales.Actor, test.Clock).ReconciliationAsync(batch))!.Consistent);
    }

    [Fact]
    public async Task MissingMaterialsDebtAndDirectWorkWritesCannotBypassCommercialDelivery()
    {
        await using var test = await InventoryScenario.CreateAsync(); var delivery = new DeliveryScenario(test);
        var work = await delivery.GroupAsync(); var w = (await delivery.SalesWork.GetAsync(work))!.Work;
        Assert.False((await delivery.SalesWork.TransitionAsync(work, w.Version, "prepare")).Succeeded);
        Assert.False((await delivery.SalesWork.EditAsync(work, new() { Version = w.Version, CustomerId = w.CustomerId, Type = w.Type, Product = w.Product, Frame = w.Frame })).Succeeded);
        Assert.False((await delivery.Fulfilment.DeliverAsync(test.OrderId, await delivery.DeliveryAsync())).Succeeded);
        var batch = await test.ReceiveAsync(); Assert.True((await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batch))).Succeeded);
        await QualityWorkflowTests.PrepareAsync(delivery.SalesWork, work);
        Assert.True((await delivery.SalesWork.SaveControlAsync(work, await QualityWorkflowTests.InputAsync(delivery.SalesWork, work, QualityStage.Technical))).Succeeded);
        Assert.False((await delivery.SalesWork.SaveControlAsync(work, await QualityWorkflowTests.InputAsync(delivery.SalesWork, work, QualityStage.Delivery))).Succeeded);
        Assert.Equal(1, (await test.Stock.BatchAsync(batch))!.OnHand);
        await delivery.PayAsync(); await delivery.ApproveAsync(work);
        Assert.True((await delivery.Fulfilment.DeliverAsync(test.OrderId, await delivery.DeliveryAsync())).Succeeded);
    }

    [Fact]
    public async Task ReplacingAReservationAfterQualityRequiresARecordedNewReview()
    {
        await using var test = await InventoryScenario.CreateAsync(); var delivery = new DeliveryScenario(test);
        var batch = await test.ReceiveAsync(2);
        Assert.True((await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batch))).Succeeded);
        var work = await delivery.GroupAsync(); await delivery.PayAsync(); await delivery.ApproveAsync(work);
        var reservation = Assert.Single((await test.Stock.ReservationsAsync(test.OrderId)).Items);
        var order = (await test.Sales.Orders.GetAsync(test.OrderId))!;
        Assert.True((await test.Stock.ReleaseAsync(test.OrderId, reservation.Id, new(Guid.NewGuid(), order.Version, reservation.Version, "Cambio ficticio de material"))).Succeeded);
        Assert.True((await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batch))).Succeeded);
        Assert.False((await delivery.Fulfilment.DeliverAsync(test.OrderId, await delivery.DeliveryAsync())).Succeeded);
        var w = (await delivery.ProWork.GetAsync(work))!.Work;
        Assert.True((await delivery.Fulfilment.ReopenAsync(test.OrderId, work, new(Guid.NewGuid(), (await delivery.Fulfilment.GetAsync(test.OrderId))!.OrderVersion, w.Version, "Revisión por cambio de material"))).Succeeded);
        await delivery.ApproveAsync(work);
        Assert.True((await delivery.Fulfilment.DeliverAsync(test.OrderId, await delivery.DeliveryAsync())).Succeeded);
        await using var db = await test.Sales.Factory.CreateDbContextAsync();
        Assert.Equal(4, await db.QualityControls.CountAsync(x => x.WorkId == work));
        Assert.Equal("Revisión por cambio de material", (await db.WorkTransitions.SingleAsync(x => x.Action == "delivery.work.reopened")).Reason);
    }
}
