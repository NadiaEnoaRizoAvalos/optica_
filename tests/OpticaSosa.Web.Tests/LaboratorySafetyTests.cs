using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Commerce;
using OpticaSosa.Application.Inventory;
using OpticaSosa.Application.Laboratory;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Domain.Laboratory;
using OpticaSosa.Infrastructure.Laboratory;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class LaboratorySafetyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ObsoleteTechnicalDataOnlyAllowsQuarantinedArrivalAndRequiresANewAuthorizedOrder(bool correctClinicalSource)
    {
        await using var test = await LaboratoryScenario.CreateAsync(); var inventory = test.Inventory; var labId = await test.AuthorizeAsync();
        await test.DispatchAsync(labId);
        var projection = (await inventory.Technical.ProjectionAsync(inventory.OrderId, inventory.LineId))!;
        var original = (await inventory.Test.Publications.GetAsync(inventory.Test.CustomerId, projection.PublicationId))!;
        if (correctClinicalSource)
        {
            var source = (await inventory.Test.Clinical.Clinical.GetAsync(inventory.Test.CustomerId, inventory.Test.RecordId))!;
            inventory.Test.Source.Version = source.Version; inventory.Test.Source.SubmissionId = Guid.NewGuid();
            inventory.Test.Source.CorrectionReason = "Corrección ficticia posterior al envío"; inventory.Test.Source.Right.Sphere = 1.375m;
            Assert.True((await inventory.Test.Clinical.Clinical.CorrectAsync(inventory.Test.CustomerId, inventory.Test.RecordId, inventory.Test.Source)).Succeeded);
        }
        else Assert.True((await inventory.Test.Publications.RevokeAsync(inventory.Test.CustomerId, original.Id, original.Version, "Revocación ficticia posterior al envío")).Succeeded);
        Assert.False((await test.Laboratory.DocumentAsync(inventory.OrderId, labId)).Succeeded);
        var arrival = await test.ReceiptAsync(labId);
        Assert.Equal(SalesResultCode.Invalid, (await test.Laboratory.ReceiveAsync(inventory.OrderId, labId, arrival)).Code);
        Assert.True((await test.Laboratory.ReceiveAsync(inventory.OrderId, labId, arrival with { Quarantined = 1 })).Succeeded);
        var batch = Assert.Single((await inventory.Stock.BatchesAsync()).Items); Assert.Equal(1, batch.OnHand); Assert.Equal(1, batch.Quarantined); Assert.Equal(0, batch.Available);
        var newPublication = await inventory.Test.Publications.PublishAsync(inventory.Test.CustomerId, inventory.Test.RecordId, await inventory.Test.InputAsync()); Assert.True(newPublication.Succeeded);
        var p = (await inventory.Test.Publications.GetAsync(inventory.Test.CustomerId, newPublication.Id!.Value))!;
        var assign = new AssignTechnicalInput(Guid.NewGuid(), (await inventory.Sales.Orders.GetAsync(inventory.OrderId))!.Version,
            p.Id, p.Version, TechnicalEyes.Right, test.AssignmentId, "Nueva autorización ficticia");
        Assert.Equal(SalesResultCode.Invalid, (await inventory.Technical.AssignAsync(inventory.OrderId, inventory.LineId, assign)).Code);
        var lab = (await test.Laboratory.GetAsync(inventory.OrderId, labId))!;
        Assert.True((await test.Laboratory.CancelAsync(inventory.OrderId, labId, new(Guid.NewGuid(), lab.OrderVersion, lab.Version, 2,
            "Detener la fabricación pendiente ficticia", "Confirmación ficticia del laboratorio"))).Succeeded);
        assign = assign with { OrderVersion = (await inventory.Sales.Orders.GetAsync(inventory.OrderId))!.Version };
        Assert.True((await inventory.Technical.AssignAsync(inventory.OrderId, inventory.LineId, assign)).Succeeded);
        var next = (await test.CreateInputAsync()) with { TechnicalAssignmentId = assign.SubmissionId };
        Assert.True((await test.Laboratory.CreateAsync(inventory.OrderId, inventory.LineId, next)).Succeeded);
        Assert.False((await test.Laboratory.GetAsync(inventory.OrderId, labId))!.TechnicalCurrent);
        Assert.Equal(test.AssignmentId, (await test.Laboratory.GetAsync(inventory.OrderId, labId))!.TechnicalAssignmentId);
        Assert.Equal(correctClinicalSource ? 1.375m : 1.125m, (await test.Laboratory.DocumentAsync(inventory.OrderId, next.SubmissionId)).Document!.Technical!.Right.Sphere);
    }
    [Fact]
    public async Task ConcurrentReceptionOfTheLastPendingUnitHasOneAtomicWinner()
    {
        await using var test = await LaboratoryScenario.CreateAsync(1); var inventory = test.Inventory; var labId = await test.AuthorizeAsync(1);
        await test.DispatchAsync(labId); var input = await test.ReceiptAsync(labId);
        var another = new LaboratoryService(inventory.Sales.Factory, inventory.Sales.Actor, inventory.Clock);
        var results = await Task.WhenAll(test.Laboratory.ReceiveAsync(inventory.OrderId, labId, input),
            another.ReceiveAsync(inventory.OrderId, labId, input with { SubmissionId = Guid.NewGuid() }));
        Assert.Single(results, x => x.Succeeded);
        Assert.Equal(1, (await test.Laboratory.GetAsync(inventory.OrderId, labId))!.Received);
        Assert.Single((await inventory.Stock.BatchesAsync()).Items); Assert.Single((await test.Laboratory.ReceiptsAsync(inventory.OrderId, labId))!.Items);
    }
    [Fact]
    public async Task LaboratoryAuditFailureRollsBackReceiptStockMovementAndAllCounters()
    {
        await using var test = await LaboratoryScenario.CreateAsync(); var inventory = test.Inventory; var labId = await test.AuthorizeAsync();
        await test.DispatchAsync(labId);
        await using var db = await inventory.Sales.Factory.CreateDbContextAsync();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectFictitiousLaboratoryAudit BEFORE INSERT ON AuditEntries WHEN NEW.Action = 'laboratory.received' BEGIN SELECT RAISE(ABORT, 'Fictitious audit failure'); END;");
        Assert.False((await test.Laboratory.ReceiveAsync(inventory.OrderId, labId, await test.ReceiptAsync(labId))).Succeeded);
        Assert.Equal(0, (await test.Laboratory.GetAsync(inventory.OrderId, labId))!.Received);
        Assert.Empty(await db.StockBatches.ToListAsync()); Assert.Empty(await db.StockMovements.ToListAsync()); Assert.Empty(await db.LaboratoryReceipts.ToListAsync());
        Assert.Empty(await db.AuditEntries.Where(x => x.Action == "inventory.received").ToListAsync());
    }
    [Fact]
    public async Task UnsentOrExcessiveReceptionIsRejectedWithoutCreatingPhysicalStock()
    {
        await using var test = await LaboratoryScenario.CreateAsync(); var inventory = test.Inventory; var labId = await test.AuthorizeAsync();
        Assert.Equal(SalesResultCode.Invalid, (await test.Laboratory.ReceiveAsync(inventory.OrderId, labId, await test.ReceiptAsync(labId))).Code);
        await test.DispatchAsync(labId);
        Assert.Equal(SalesResultCode.Invalid, (await test.Laboratory.ReceiveAsync(inventory.OrderId, labId, await test.ReceiptAsync(labId, 4))).Code);
        Assert.Empty((await inventory.Stock.BatchesAsync()).Items);
        var lab = (await test.Laboratory.GetAsync(inventory.OrderId, labId))!;
        Assert.Equal(3, lab.Pending); Assert.Equal(0, lab.Received);
    }
    [Fact]
    public async Task ObservedMaterialDoesNotCoverTheOrderAndAllowsAnExplicitReplacementWithoutErasingTheFirstReceipt()
    {
        await using var test = await LaboratoryScenario.CreateAsync(1); var inventory = test.Inventory; var labId = await test.AuthorizeAsync(1);
        await test.DispatchAsync(labId);
        Assert.True((await test.Laboratory.ReceiveAsync(inventory.OrderId, labId, await test.ReceiptAsync(labId, 1, 1))).Succeeded);
        var replacement = await test.AuthorizeAsync(1);
        Assert.NotEqual(labId, replacement); Assert.Single((await test.Laboratory.ReceiptsAsync(inventory.OrderId, labId))!.Items);
        Assert.Equal(1, Assert.Single((await inventory.Stock.BatchesAsync()).Items).Quarantined);
        Assert.Equal(0, Assert.Single((await inventory.Stock.AvailabilityAsync(inventory.OrderId)).Items).Available);
    }
}
