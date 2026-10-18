using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Commerce;
using OpticaSosa.Application.Laboratory;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Commerce;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Infrastructure.Laboratory;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class LaboratoryBoundaryTests
{
    [Fact]
    public async Task ProfessionalReadsTechnicalStatusAndDocumentWithoutPricesButCannotPerformCommercialCommands()
    {
        await using var test = await LaboratoryScenario.CreateAsync(); var inventory = test.Inventory; var labId = await test.AuthorizeAsync();
        var professional = new LaboratoryService(inventory.Sales.Factory, inventory.Test.Clinical.Actor, inventory.Clock);
        var list = await professional.ListAsync(inventory.OrderId); Assert.Single(list.Items);
        Assert.DoesNotContain("price", JsonSerializer.Serialize(list), StringComparison.OrdinalIgnoreCase);
        Assert.True((await professional.DocumentAsync(inventory.OrderId, labId)).Succeeded);
        var lab = (await professional.GetAsync(inventory.OrderId, labId))!;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await professional.CreateAsync(inventory.OrderId, inventory.LineId, await test.CreateInputAsync()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => professional.RecordDispatchAsync(inventory.OrderId, labId, new(Guid.NewGuid(), lab.OrderVersion, lab.Version, "Ficticio")));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await professional.ReceiveAsync(inventory.OrderId, labId, await test.ReceiptAsync(labId)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => professional.CancelAsync(inventory.OrderId, labId, new(Guid.NewGuid(), lab.OrderVersion, lab.Version, 1, "Ficticio")));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => professional.RescheduleAsync(inventory.OrderId, labId, new(Guid.NewGuid(), lab.OrderVersion, lab.Version, null, "Ficticio")));
        Assert.Null(await professional.GetAsync(Guid.NewGuid(), labId)); Assert.Null(await professional.ReceiptsAsync(Guid.NewGuid(), labId));
    }
    [Fact]
    public async Task NonClinicalLaboratoryOrdersNeedNoPrescriptionButTheirStockCannotBeReassignedImplicitly()
    {
        await using var test = await LaboratoryScenario.CreateAsync(1, false, ProductCategory.Frame); var inventory = test.Inventory; var labId = await test.AuthorizeAsync(1);
        Assert.Null((await test.Laboratory.DocumentAsync(inventory.OrderId, labId)).Document!.Technical);
        await test.DispatchAsync(labId); Assert.True((await test.Laboratory.ReceiveAsync(inventory.OrderId, labId, await test.ReceiptAsync(labId))).Succeeded);
        var batch = Assert.Single((await inventory.Stock.BatchesAsync()).Items);
        var otherOrder = await inventory.Sales.ConfirmAsync(await inventory.Sales.DraftAsync(inventory.Catalog, inventory.Test.CustomerId, inventory.VariantId));
        var otherLine = Assert.Single((await inventory.Sales.Orders.LinesAsync(otherOrder))!.Items);
        var request = await inventory.ReserveInputAsync(batch.Id);
        request = request with { OrderVersion = (await inventory.Sales.Orders.GetAsync(otherOrder))!.Version };
        Assert.Equal(SalesResultCode.Invalid, (await inventory.Stock.ReserveAsync(otherOrder, otherLine.Id, request)).Code);
        Assert.True((await inventory.Stock.ReserveAsync(inventory.OrderId, inventory.LineId, await inventory.ReserveInputAsync(batch.Id))).Succeeded);
        Assert.Equal(0, Assert.Single((await inventory.Stock.AvailabilityAsync(otherOrder)).Items).Available);
    }
    [Fact]
    public async Task ContactReceptionRequiresLotExpiryAndClosedBoxUnits()
    {
        await using var test = await LaboratoryScenario.CreateAsync(1, category: ProductCategory.ContactLens); var inventory = test.Inventory; var labId = await test.AuthorizeAsync(1);
        await test.DispatchAsync(labId); var receipt = await test.ReceiptAsync(labId);
        Assert.Equal(SalesResultCode.Invalid, (await test.Laboratory.ReceiveAsync(inventory.OrderId, labId, receipt)).Code);
        Assert.True((await test.Laboratory.ReceiveAsync(inventory.OrderId, labId, receipt with { Lot = "LOTE-FIC", Expiry = new(2027, 1, 1), ExpiryPrecision = ExpiryPrecision.Month })).Succeeded);
        var batch = Assert.Single((await inventory.Stock.BatchesAsync()).Items);
        Assert.Equal(new DateOnly(2027, 1, 31), batch.ExpiresOn); Assert.Equal(1, batch.OnHand);
        var line = Assert.Single((await inventory.Sales.Orders.LinesAsync(inventory.OrderId))!.Items);
        Assert.Equal(ProductUnit.ClosedBox, line.Unit); Assert.Equal(6, line.UnitsPerPackage);
    }
    [Fact]
    public async Task LaboratoryCrystalsCannotMixEyesWithinOneCommercialLine()
    {
        await using var test = await LaboratoryScenario.CreateAsync(2, assign: false); var inventory = test.Inventory;
        var source = await inventory.Test.InputAsync(); source.IncludeLeft = true;
        var published = await inventory.Test.Publications.PublishAsync(inventory.Test.CustomerId, inventory.Test.RecordId, source); Assert.True(published.Succeeded);
        var pub = (await inventory.Test.Publications.GetAsync(inventory.Test.CustomerId, published.Id!.Value))!;
        var result = await inventory.Technical.AssignAsync(inventory.OrderId, inventory.LineId, new(Guid.NewGuid(),
            (await inventory.Sales.Orders.GetAsync(inventory.OrderId))!.Version, pub.Id, pub.Version, TechnicalEyes.Both));
        Assert.Equal(SalesResultCode.Invalid, result.Code); Assert.Contains("separadas", result.Error);
    }
    [Fact]
    public async Task ReschedulingHistoryIsPaginatedAndSqliteRejectsImpossibleCounters()
    {
        await using var test = await LaboratoryScenario.CreateAsync(); var inventory = test.Inventory; var labId = await test.AuthorizeAsync();
        for (var i = 0; i < 52; i++)
        {
            var lab = (await test.Laboratory.GetAsync(inventory.OrderId, labId))!;
            Assert.True((await test.Laboratory.RescheduleAsync(inventory.OrderId, labId, new(Guid.NewGuid(), lab.OrderVersion, lab.Version,
                new DateOnly(2026, 10, 20).AddDays(i), "Reprogramación ficticia"))).Succeeded);
        }
        var first = (await test.Laboratory.ChangesAsync(inventory.OrderId, labId))!;
        var second = (await test.Laboratory.ChangesAsync(inventory.OrderId, labId, 2))!;
        Assert.Equal(50, first.Items.Count); Assert.True(first.HasMore); Assert.Equal(2, second.Items.Count); Assert.False(second.HasMore);
        Assert.Empty(first.Items.Select(x => x.Id).Intersect(second.Items.Select(x => x.Id)));
        await using var db = await inventory.Sales.Factory.CreateDbContextAsync();
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE LaboratoryOrders SET ReceivedQuantity = Quantity + 1 WHERE Id = {labId}"));
        Assert.Equal(0, (await test.Laboratory.GetAsync(inventory.OrderId, labId))!.Received);
    }
}
