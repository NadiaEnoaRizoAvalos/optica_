using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Inventory;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Domain.Work;
using OpticaSosa.Infrastructure.Inventory;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class InventoryReturnTests
{
    [Fact]
    public async Task ReturnsRequireRealDeliveryEnterQuarantineAndDoNotRefundMoney()
    {
        await using var test = await InventoryScenario.CreateAsync(quantity: 2);
        var batchId = await test.ReceiveAsync(2);
        Assert.True((await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batchId, 2))).Succeeded);
        var service = new InventoryAdministration(test.Sales.Factory, test.Sales.Actor, test.Clock);
        await using var db = await test.Sales.Factory.CreateDbContextAsync();
        var batch = await db.StockBatches.SingleAsync(x => x.Id == batchId);
        var reservation = await db.StockReservations.SingleAsync();
        var now = test.Clock.GetUtcNow().UtcDateTime; var actor = test.Test.Clinical.Actor.Id!;
        batch.Consume(2, DateOnly.FromDateTime(now));
        reservation.Close(ReservationStatus.Consumed, actor, now, "Consumo ficticio para trabajo");
        db.StockMovements.Add(new(Guid.NewGuid(), batch.Id, reservation.Id, StockMovementKind.Consume,
            -2, -2, 0, "Consumo ficticio", new string('0', 64), actor, now));
        await db.SaveChangesAsync();
        var input = new InventoryReturnInput(Guid.NewGuid(), batch.Version, reservation.Version, 1, "Devolución física ficticia");
        Assert.False((await service.ReturnFromCustomerAsync(reservation.Id, input)).Succeeded);
        var work = new OpticalWork(Guid.NewGuid(), test.Test.CustomerId, OpticalWorkType.FrameOnly,
            "Armazón ficticio", "Modelo ficticio", null, actor, now);
        foreach (var command in new[] { "prepare", "control" })
        {
            var previous = work.Status; work.Transition(command);
            db.WorkTransitions.Add(new(work, previous, actor, now, command));
        }
        foreach (var stage in Enum.GetValues<QualityStage>())
        {
            var rules = QualityCatalog.For(work.Type, stage);
            var values = rules.Select(x => new CheckValue(x.Key, x.Applicable ? CheckStatus.Conforming : CheckStatus.NotApplicable,
                x.Applicable ? null : "No corresponde al armazón ficticio")).ToArray();
            var control = new QualityControl(Guid.NewGuid(), work, stage, rules, values, true, actor,
                ResponsibleRole.Professional, now, "Control ficticio completo", Guid.NewGuid());
            work.Apply(control); db.QualityControls.Add(control);
        }
        var ready = work.Status; work.Transition("deliver");
        db.WorkTransitions.Add(new(work, ready, actor, now, "deliver"));
        db.OpticalWorks.Add(work); db.StockDeliveryProofs.Add(new(reservation, work));
        await db.SaveChangesAsync();
        Assert.True((await service.ReturnFromCustomerAsync(reservation.Id, input)).Succeeded);
        Assert.True((await service.ReturnFromCustomerAsync(reservation.Id, input)).Succeeded);
        var returned = (await test.Stock.BatchAsync(batch.Id))!;
        Assert.Equal(1, returned.OnHand); Assert.Equal(1, returned.Quarantined); Assert.Equal(0, returned.Available);
        Assert.False((await service.ReturnFromCustomerAsync(reservation.Id, input with { SubmissionId = Guid.NewGuid(),
            BatchVersion = returned.Version, Quantity = 2 })).Succeeded);
        Assert.Single((await service.ReturnsAsync(reservation.Id)).Items);
        Assert.True((await service.ReconciliationAsync(batch.Id))!.Consistent);
        Assert.Empty(await db.OrderMoneyEntries.ToListAsync());
        Assert.Equal(2, await db.QualityControls.CountAsync(x => x.WorkId == work.Id));
        var hold = Assert.Single((await service.HoldsAsync(batch.Id)).Items);
        Assert.True((await service.ReviewAsync(batch.Id, hold.Id, new(Guid.NewGuid(), returned.Version,
            hold.Version, 1, true, true, true, null, "Armazón revisado sin daños"))).Succeeded);
        Assert.Equal(1, (await test.Stock.BatchAsync(batch.Id))!.Available);
        Assert.True((await service.ReconciliationAsync(batch.Id))!.Consistent);
    }
}
