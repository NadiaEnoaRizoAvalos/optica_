using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Commerce;
using OpticaSosa.Application.Laboratory;
using OpticaSosa.Domain.Laboratory;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class LaboratoryOrderTests
{
    [Fact]
    public async Task TechnicalAndCommercialAuthorizationsAreBothRequiredWithoutRequiringADeposit()
    {
        await using var test = await LaboratoryScenario.CreateAsync(assign: false); var inventory = test.Inventory;
        var input = await test.CreateInputAsync();
        Assert.Equal(SalesResultCode.Invalid, (await test.Laboratory.CreateAsync(inventory.OrderId, inventory.LineId, input)).Code);
        var assignment = await inventory.AssignAsync(); input = (await test.CreateInputAsync()) with { TechnicalAssignmentId = assignment, CommercialConfirmed = false };
        Assert.Equal(SalesResultCode.Invalid, (await test.Laboratory.CreateAsync(inventory.OrderId, inventory.LineId, input)).Code);
        input = input with { CommercialConfirmed = true };
        Assert.True((await test.Laboratory.CreateAsync(inventory.OrderId, inventory.LineId, input)).Succeeded);
        Assert.True((await test.Laboratory.CreateAsync(inventory.OrderId, inventory.LineId, input)).Succeeded);
        Assert.Equal(0, (await inventory.Sales.Orders.GetAsync(inventory.OrderId))!.NetPaidMinor);
        Assert.Empty((await inventory.Stock.BatchesAsync()).Items);
        Assert.Equal(SalesResultCode.Invalid, (await test.Laboratory.CreateAsync(inventory.OrderId, inventory.LineId, await test.CreateInputAsync(1))).Code);
    }
    [Fact]
    public async Task DocumentationIsMinimalAndDoesNotRecordDispatchOrReserveStock()
    {
        await using var test = await LaboratoryScenario.CreateAsync(); var inventory = test.Inventory; var labId = await test.AuthorizeAsync();
        var before = (await test.Laboratory.GetAsync(inventory.OrderId, labId))!;
        var document = await test.Laboratory.DocumentAsync(inventory.OrderId, labId); Assert.True(document.Succeeded, document.Error);
        var json = JsonSerializer.Serialize(document.Document);
        foreach (var forbidden in new[] { "NOTA_CLINICA_FICTICIA_NO_PUBLICABLE", "SINTOMA_FICTICIO_NO_PUBLICABLE", "Price", "CorrectionReason", "RevocationReason", "DocumentKey" })
            Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1.125m, document.Document!.Technical!.Right.Sphere);
        var after = (await test.Laboratory.GetAsync(inventory.OrderId, labId))!;
        Assert.Equal(LaboratoryStatus.Authorized, after.Status); Assert.Null(after.SentAtUtc); Assert.Equal(before.Version, after.Version);
        var send = new LaboratoryDispatchInput(Guid.NewGuid(), after.OrderVersion, after.Version, "Constancia ficticia");
        Assert.True((await test.Laboratory.RecordDispatchAsync(inventory.OrderId, labId, send)).Succeeded);
        Assert.True((await test.Laboratory.RecordDispatchAsync(inventory.OrderId, labId, send)).Succeeded);
        after = (await test.Laboratory.GetAsync(inventory.OrderId, labId))!; Assert.Equal(LaboratoryStatus.Sent, after.Status);
        Assert.Equal(SalesResultCode.Invalid, (await test.Laboratory.RecordDispatchAsync(inventory.OrderId, labId,
            new(Guid.NewGuid(), after.OrderVersion, after.Version, "Otro envío ficticio"))).Code);
        Assert.Empty((await inventory.Stock.BatchesAsync()).Items);
    }
    [Fact]
    public async Task PartialReceiptUsesExistingInventoryOnceAndDoesNotApproveQualityOrDelivery()
    {
        await using var test = await LaboratoryScenario.CreateAsync(); var inventory = test.Inventory; var labId = await test.AuthorizeAsync();
        await test.DispatchAsync(labId);
        var input = await test.ReceiptAsync(labId, 2, 1);
        Assert.True((await test.Laboratory.ReceiveAsync(inventory.OrderId, labId, input)).Succeeded);
        Assert.True((await test.Laboratory.ReceiveAsync(inventory.OrderId, labId, input)).Succeeded);
        var lab = (await test.Laboratory.GetAsync(inventory.OrderId, labId))!;
        Assert.Equal(2, lab.Received); Assert.Equal(1, lab.Quarantined); Assert.Equal(1, lab.Pending); Assert.Equal(LaboratoryStatus.PartiallyReceived, lab.Status);
        var batch = Assert.Single((await inventory.Stock.BatchesAsync()).Items);
        Assert.Equal(2, batch.OnHand); Assert.Equal(1, batch.Quarantined); Assert.Equal(1, batch.Available);
        Assert.Equal(1, Assert.Single((await inventory.Stock.AvailabilityAsync(inventory.OrderId)).Items).Available);
        Assert.True((await inventory.Stock.ReserveAsync(inventory.OrderId, inventory.LineId, await inventory.ReserveInputAsync(batch.Id, assignment: test.AssignmentId))).Succeeded);
        Assert.True((await test.Laboratory.ReceiveAsync(inventory.OrderId, labId, await test.ReceiptAsync(labId))).Succeeded);
        lab = (await test.Laboratory.GetAsync(inventory.OrderId, labId))!; Assert.Equal(LaboratoryStatus.Received, lab.Status);
        Assert.Equal(2, (await test.Laboratory.ReceiptsAsync(inventory.OrderId, labId))!.Items.Count);
        await using var db = await inventory.Sales.Factory.CreateDbContextAsync();
        Assert.Empty(await db.QualityControls.ToListAsync()); Assert.Empty(await db.OpticalWorks.ToListAsync());
        Assert.Equal(3, (await inventory.Stock.BatchesAsync()).Items.Sum(x => x.OnHand));
    }
    [Fact]
    public async Task CancellationOnlyRemovesPendingQuantitiesAndRequiresLaboratoryConfirmationAfterDispatch()
    {
        await using var test = await LaboratoryScenario.CreateAsync(); var inventory = test.Inventory; var labId = await test.AuthorizeAsync();
        await test.DispatchAsync(labId);
        Assert.True((await test.Laboratory.ReceiveAsync(inventory.OrderId, labId, await test.ReceiptAsync(labId))).Succeeded);
        var lab = (await test.Laboratory.GetAsync(inventory.OrderId, labId))!;
        var cancel = new LaboratoryCancelInput(Guid.NewGuid(), lab.OrderVersion, lab.Version, 2, "Cancelación ficticia");
        Assert.Equal(SalesResultCode.Invalid, (await test.Laboratory.CancelAsync(inventory.OrderId, labId, cancel)).Code);
        Assert.Equal(SalesResultCode.Invalid, (await test.Laboratory.CancelAsync(inventory.OrderId, labId, cancel with { Quantity = 3, LaboratoryConfirmation = "Confirmación ficticia" })).Code);
        Assert.Equal(SalesResultCode.Invalid, (await inventory.Sales.Orders.CancelAsync(inventory.OrderId, new(Guid.NewGuid(), lab.OrderVersion, "Ficticio"))).Code);
        cancel = cancel with { LaboratoryConfirmation = "Confirmación ficticia de cantidades no recibidas" };
        Assert.True((await test.Laboratory.CancelAsync(inventory.OrderId, labId, cancel)).Succeeded);
        Assert.True((await test.Laboratory.CancelAsync(inventory.OrderId, labId, cancel)).Succeeded);
        lab = (await test.Laboratory.GetAsync(inventory.OrderId, labId))!;
        Assert.Equal(1, lab.Received); Assert.Equal(2, lab.Cancelled); Assert.Equal(LaboratoryStatus.Closed, lab.Status);
        Assert.Equal(1, Assert.Single((await inventory.Stock.BatchesAsync()).Items).OnHand);
        Assert.Single((await test.Laboratory.ChangesAsync(inventory.OrderId, labId))!.Items);
        Assert.Empty((await inventory.Sales.Payments.ListAsync(inventory.OrderId))!.Items);
    }
    [Fact]
    public async Task ReschedulingKeepsPreviousDatesAndMakesDelaysVisibleWithoutChangingQuantities()
    {
        await using var test = await LaboratoryScenario.CreateAsync(); var inventory = test.Inventory; var labId = await test.AuthorizeAsync();
        inventory.Clock.Now = inventory.Clock.Now.AddDays(4);
        var lab = (await test.Laboratory.GetAsync(inventory.OrderId, labId))!; Assert.True(lab.Delayed);
        Assert.True((await test.Laboratory.RescheduleAsync(inventory.OrderId, labId, new(Guid.NewGuid(), lab.OrderVersion, lab.Version,
            new(2026, 10, 25), "Demora ficticia informada"))).Succeeded);
        lab = (await test.Laboratory.GetAsync(inventory.OrderId, labId))!; Assert.False(lab.Delayed); Assert.Equal(3, lab.Pending);
        var change = Assert.Single((await test.Laboratory.ChangesAsync(inventory.OrderId, labId))!.Items);
        Assert.Equal(new DateOnly(2026, 10, 20), change.PreviousEstimatedDate); Assert.Equal(new DateOnly(2026, 10, 25), change.NewEstimatedDate);
    }
}
