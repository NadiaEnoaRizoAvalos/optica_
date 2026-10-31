using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Commerce;
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

public sealed class OrderDeliverySafetyTests
{
    [Fact]
    public async Task DirectHttpCannotDeliverOneCommercialWorkEvenWhenBothControlsAreApproved()
    {
        await using var test = await InventoryScenario.CreateAsync(); var delivery = new DeliveryScenario(test);
        var batch = await test.ReceiveAsync();
        Assert.True((await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batch))).Succeeded);
        var work = await delivery.GroupAsync(); await delivery.PayAsync(); await delivery.ApproveAsync(work);
        using var browser = test.Test.App.Browser(); await BrowserForms.LoginAsync(browser, "ventas.ficticio");
        var path = $"/pedidos/{work}"; var html = await browser.GetStringAsync(path);
        Assert.DoesNotContain("value=\"deliver\"", html);
        var current = (await delivery.SalesWork.GetAsync(work))!.Work;
        var response = await BrowserForms.PostAsync(browser, path, path, new() { ["Version"] = current.Version.ToString(), ["Command"] = "deliver" });
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(WorkStatus.ReadyForDelivery, (await delivery.SalesWork.GetAsync(work))!.Work.Status);
        Assert.Equal(1, (await test.Stock.BatchAsync(batch))!.OnHand);
        await using var db = await test.Sales.Factory.CreateDbContextAsync();
        Assert.Empty(await db.Set<OrderDelivery>().ToListAsync());
    }

    [Fact]
    public async Task ConcurrentCommandsAndAuditFailureCannotPartiallyConsumeOrDeliver()
    {
        await using var test = await InventoryScenario.CreateAsync(); var delivery = new DeliveryScenario(test);
        var batch = await test.ReceiveAsync();
        Assert.True((await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batch))).Succeeded);
        var work = await delivery.GroupAsync(); await delivery.PayAsync(); await delivery.ApproveAsync(work);
        var input = await delivery.DeliveryAsync();
        await using var db = await test.Sales.Factory.CreateDbContextAsync();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectFictitiousDelivery BEFORE INSERT ON AuditEntries WHEN NEW.Action = 'delivery.order.completed' BEGIN SELECT RAISE(ABORT, 'Fictitious audit failure'); END;");
        Assert.False((await delivery.Fulfilment.DeliverAsync(test.OrderId, input)).Succeeded);
        Assert.Equal(1, (await test.Stock.BatchAsync(batch))!.OnHand);
        Assert.Equal(WorkStatus.ReadyForDelivery, (await delivery.ProWork.GetAsync(work))!.Work.Status);
        Assert.Empty(await db.Set<OrderDelivery>().ToListAsync()); Assert.Empty(await db.StockDeliveryProofs.ToListAsync());
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectFictitiousDelivery");
        var results = await Task.WhenAll(delivery.Fulfilment.DeliverAsync(test.OrderId, input),
            delivery.Fulfilment.DeliverAsync(test.OrderId, input with { SubmissionId = Guid.NewGuid() }));
        Assert.Single(results, x => x.Succeeded);
        Assert.Equal(0, (await test.Stock.BatchAsync(batch))!.OnHand);
        Assert.Single(await db.Set<OrderDelivery>().ToListAsync());
        Assert.Single(await db.StockMovements.Where(x => x.Kind == StockMovementKind.Consume).ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RevokedOrCorrectedClinicalSourceBlocksApprovedDeliveryAndPreservesHistory(bool correct)
    {
        await using var test = await InventoryScenario.CreateAsync(ProductCategory.Lens); var delivery = new DeliveryScenario(test);
        var assignment = await test.AssignAsync(); var batch = await test.ReceiveAsync();
        Assert.True((await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batch, assignment: assignment))).Succeeded);
        var work = await delivery.GroupAsync(assignment); await delivery.PayAsync(); await delivery.ApproveAsync(work);
        if (correct)
        {
            var old = (await test.Test.Clinical.Clinical.GetAsync(test.Test.CustomerId, test.Test.RecordId))!;
            test.Test.Source.Version = old.Version; test.Test.Source.SubmissionId = Guid.NewGuid(); test.Test.Source.CorrectionReason = "Corrección ficticia posterior a control";
            test.Test.Source.Right.Sphere = 1.375m;
            Assert.True((await test.Test.Clinical.Clinical.CorrectAsync(test.Test.CustomerId, test.Test.RecordId, test.Test.Source)).Succeeded);
        }
        else
        {
            var projection = (await test.Technical.ProjectionAsync(test.OrderId, test.LineId))!;
            var source = (await test.Test.Publications.GetAsync(test.Test.CustomerId, projection.PublicationId))!;
            Assert.True((await test.Test.Publications.RevokeAsync(test.Test.CustomerId, source.Id, source.Version, "Revocación ficticia")).Succeeded);
        }
        Assert.False((await delivery.Fulfilment.DeliverAsync(test.OrderId, await delivery.DeliveryAsync())).Succeeded);
        Assert.Equal(1, (await test.Stock.BatchAsync(batch))!.Reserved);
        await using var db = await test.Sales.Factory.CreateDbContextAsync();
        Assert.Equal(2, await db.QualityControls.CountAsync(x => x.WorkId == work)); Assert.Empty(await db.Set<OrderDelivery>().ToListAsync());
    }

    [Fact]
    public async Task ContactExpiryAndOpenedPhysicalReturnRemainBlockedWithoutChangingPayments()
    {
        await using var test = await InventoryScenario.CreateAsync(ProductCategory.ContactLens); var delivery = new DeliveryScenario(test);
        var assignment = await test.AssignAsync(); var batch = await test.ReceiveAsync(lot: "LOTE-FIC", expiry: new(2026, 10, 17), precision: ExpiryPrecision.Day);
        Assert.True((await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batch, assignment: assignment))).Succeeded);
        var work = await delivery.GroupAsync(assignment); await delivery.PayAsync(); await delivery.ApproveAsync(work);
        test.Clock.Now = test.Clock.Now.AddDays(1);
        Assert.False((await delivery.Fulfilment.DeliverAsync(test.OrderId, await delivery.DeliveryAsync())).Succeeded);
        test.Clock.Now = test.Clock.Now.AddDays(-1);
        Assert.True((await delivery.Fulfilment.DeliverAsync(test.OrderId, await delivery.DeliveryAsync())).Succeeded);
        var reservation = Assert.Single((await test.Stock.ReservationsAsync(test.OrderId)).Items);
        var stock = new InventoryAdministration(test.Sales.Factory, test.Sales.Actor, test.Clock);
        Assert.True((await stock.ReturnFromCustomerAsync(reservation.Id, new(Guid.NewGuid(), (await test.Stock.BatchAsync(batch))!.Version,
            reservation.Version, 1, "Caja ficticia abierta devuelta", true))).Succeeded);
        var clinicalStock = new InventoryAdministration(test.Sales.Factory, test.Test.Clinical.Actor, test.Clock);
        var hold = Assert.Single((await stock.HoldsAsync(batch)).Items);
        Assert.False((await clinicalStock.ReviewAsync(batch, hold.Id, new(Guid.NewGuid(), (await test.Stock.BatchAsync(batch))!.Version,
            hold.Version, 1, true, true, true, true, "No liberar una caja conocida abierta"))).Succeeded);
        Assert.Equal(1, (await test.Stock.BatchAsync(batch))!.Quarantined);
        Assert.Equal(0, (await test.Sales.Orders.GetAsync(test.OrderId))!.RefundedMinor);
    }

    [Fact]
    public async Task AdministratorNeedsOperationalRoleAndProfessionalReadsNoPricesOrClinicalNotesFromFulfilment()
    {
        await using var test = await InventoryScenario.CreateAsync(ProductCategory.Lens); var delivery = new DeliveryScenario(test);
        var assignment = await test.AssignAsync();
        using var admin = await CommerceScenario.OpenAsync(test.Test.App);
        var administrator = new OrderFulfilmentService(admin.Factory, admin.Actor, test.Clock);
        var professional = new OrderFulfilmentService(test.Sales.Factory, test.Test.Clinical.Actor, test.Clock);
        var input = new CommercialWorkInput(Guid.NewGuid(), (await professional.GetAsync(test.OrderId))!.OrderVersion,
            [new(test.LineId, assignment)], "Armazón ficticio");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => administrator.CreateWorkAsync(test.OrderId, input));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => administrator.DeliverAsync(test.OrderId, new(Guid.NewGuid(), input.OrderVersion, [])));
        Assert.True((await professional.CreateWorkAsync(test.OrderId, input)).Succeeded);
        var json = JsonSerializer.Serialize(new { Summary = await professional.GetAsync(test.OrderId), Materials = await professional.MaterialsAsync(test.OrderId), Works = await professional.WorksAsync(test.OrderId) });
        foreach (var forbidden in new[] { "UnitPrice", "TotalMinor", "ReceivedMinor", "NOTA_CLINICA_FICTICIA_NO_PUBLICABLE", "SINTOMA_FICTICIO_NO_PUBLICABLE" }) Assert.DoesNotContain(forbidden, json);
        using var salesClinical = await ClinicalScenario.OpenAsync(test.Test.App, "ventas.ficticio");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => salesClinical.Clinical.GetAsync(test.Test.CustomerId, test.Test.RecordId));
    }

    [Fact]
    public async Task DeliveredOrderRejectsNewPhysicalActionsButKeepsExplicitMonetaryRefundCircuit()
    {
        await using var test = await InventoryScenario.CreateAsync(); var delivery = new DeliveryScenario(test);
        var batch = await test.ReceiveAsync(2); Assert.True((await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batch))).Succeeded);
        var work = await delivery.GroupAsync(); await delivery.PayAsync(); await delivery.ApproveAsync(work);
        Assert.True((await delivery.Fulfilment.DeliverAsync(test.OrderId, await delivery.DeliveryAsync())).Succeeded);
        Assert.False((await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batch))).Succeeded);
        var order = (await test.Sales.Orders.GetAsync(test.OrderId))!;
        Assert.False((await test.Sales.Orders.CancelAsync(test.OrderId, new(Guid.NewGuid(), order.Version, "No cancelar una entrega"))).Succeeded);
        using var admin = await CommerceScenario.OpenAsync(test.Test.App);
        var payment = Assert.Single((await admin.Payments.ListAsync(test.OrderId))!.Items);
        Assert.True((await admin.Payments.RefundAsync(test.OrderId, new(Guid.NewGuid(), order.Version, payment.Id, 10m, "ARS", "Medio ficticio", "Devolución monetaria explícita"))).Succeeded);
        Assert.Equal(FulfilmentState.Delivered, (await delivery.Fulfilment.GetAsync(test.OrderId))!.State);
        Assert.Equal(1, (await test.Stock.BatchAsync(batch))!.OnHand);
    }
}
