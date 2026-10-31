using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Commerce;
using OpticaSosa.Application.Delivery;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Commerce;
using OpticaSosa.Domain.Delivery;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Domain.Work;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class OrderWorkGroupingTests
{
    [Fact]
    public async Task MixedOrderGroupsFrameAndBothLabEyesButDeliversOnlyWhenEveryIndependentWorkIsReady()
    {
        await using var lab = await LaboratoryScenario.CreateAsync(quantity: 1); var test = lab.Inventory; var helper = new DeliveryScenario(test);
        var frameProduct = await CatalogTests.ProductAsync(test.Catalog, ProductCategory.Frame, "Armazón ficticio del conjunto");
        var frameVariant = await test.Catalog.Catalog.CreateVariantAsync(frameProduct, await CatalogTests.InputAsync(test.Catalog, frameProduct, "FRAME-FICTICIO"));
        Assert.True(frameVariant.Succeeded); var frame = (Product: frameProduct, Variant: frameVariant.Id!.Value);
        var accessoryProduct = await CatalogTests.ProductAsync(test.Catalog, ProductCategory.Accessory, "Accesorio ficticio independiente");
        var accessoryVariant = await test.Catalog.Catalog.CreateVariantAsync(accessoryProduct, await CatalogTests.InputAsync(test.Catalog, accessoryProduct, "ACCESSORY-FICTICIO"));
        Assert.True(accessoryVariant.Succeeded); var accessory = (Product: accessoryProduct, Variant: accessoryVariant.Id!.Value);
        var draft = await test.Sales.DraftAsync(test.Catalog, test.Test.CustomerId, test.VariantId);
        var frameDraft = await test.Sales.DraftAsync(test.Catalog, test.Test.CustomerId, frame.Variant);
        var accessoryDraft = await test.Sales.DraftAsync(test.Catalog, test.Test.CustomerId, accessory.Variant);
        draft.Lines = [draft.Lines[0] with { Source = FulfilmentSource.Laboratory }, draft.Lines[0] with { Source = FulfilmentSource.Laboratory }, frameDraft.Lines[0], accessoryDraft.Lines[0]];
        var orderId = await test.Sales.ConfirmAsync(draft); var lines = (await test.Sales.Orders.LinesAsync(orderId))!.Items;
        var publicationInput = await test.Test.InputAsync(); publicationInput.IncludeLeft = true;
        var published = await test.Test.Publications.PublishAsync(test.Test.CustomerId, test.Test.RecordId, publicationInput);
        Assert.True(published.Succeeded, published.Error);
        var publication = (await test.Test.Publications.GetAsync(test.Test.CustomerId, published.Id!.Value))!;
        var materials = new List<WorkMaterialInput>();
        for (var i = 0; i < 2; i++)
        {
            var assignment = Guid.NewGuid();
            var assigned = await test.Technical.AssignAsync(orderId, lines[i].Id, new(assignment, (await test.Sales.Orders.GetAsync(orderId))!.Version,
                publication.Id, publication.Version, i == 0 ? TechnicalEyes.Right : TechnicalEyes.Left));
            Assert.True(assigned.Succeeded, assigned.Error); materials.Add(new(lines[i].Id, assignment));
            var supplier = (await test.Catalog.Suppliers.GetAsync(lab.SupplierId))!; var labId = Guid.NewGuid();
            Assert.True((await lab.Laboratory.CreateAsync(orderId, lines[i].Id, new(labId, (await test.Sales.Orders.GetAsync(orderId))!.Version,
                supplier.Supplier.Id, supplier.Version, assignment, 1, new(2026, 10, 20), true))).Succeeded);
            var item = (await lab.Laboratory.GetAsync(orderId, labId))!;
            Assert.True((await lab.Laboratory.RecordDispatchAsync(orderId, labId, new(Guid.NewGuid(), item.OrderVersion, item.Version, "Envío ficticio"))).Succeeded);
            item = (await lab.Laboratory.GetAsync(orderId, labId))!;
            Assert.True((await lab.Laboratory.ReceiveAsync(orderId, labId, new(Guid.NewGuid(), item.OrderVersion, item.Version, 1, 0, null, null,
                ExpiryPrecision.None, "Recepción ficticia", null))).Succeeded);
            var batch = Assert.Single((await test.Stock.BatchesAsync(test.VariantId)).Items, b => b.Reserved == 0 &&
                // Cada recepción se reserva inmediatamente para su ojo antes de continuar.
                b.Available == 1);
            Assert.True((await test.Stock.ReserveAsync(orderId, lines[i].Id, new(Guid.NewGuid(), (await test.Sales.Orders.GetAsync(orderId))!.Version,
                batch.Id, batch.Version, assignment, 1))).Succeeded);
        }
        foreach (var line in lines.Skip(2))
        {
            var variant = (await test.Catalog.Catalog.VariantAsync(line.Id == lines[2].Id ? frame.Product : accessory.Product, line.VariantId))!;
            var received = await test.Stock.ReceiveAsync(new(Guid.NewGuid(), line.VariantId, variant.Technical.Version, 1, 0, null, null, ExpiryPrecision.None, "Stock ficticio"));
            Assert.True(received.Succeeded, received.Error); var batch = (await test.Stock.BatchAsync(received.BatchId!.Value))!;
            Assert.True((await test.Stock.ReserveAsync(orderId, line.Id, new(Guid.NewGuid(), (await test.Sales.Orders.GetAsync(orderId))!.Version, batch.Id, batch.Version, null, 1))).Succeeded);
        }
        materials.Add(new(lines[2].Id)); var glasses = Guid.NewGuid();
        var created = await helper.Fulfilment.CreateWorkAsync(orderId, new(glasses, (await test.Sales.Orders.GetAsync(orderId))!.Version, materials));
        Assert.True(created.Succeeded, created.Error);
        var sale = (await test.Sales.Orders.GetAsync(orderId))!;
        Assert.True((await test.Sales.Payments.ReceiveAsync(orderId, new(Guid.NewGuid(), sale.Version, sale.BalanceMinor / 100m, sale.Currency, "Medio ficticio"))).Succeeded);
        await helper.ApproveAsync(glasses);
        Assert.False((await helper.Fulfilment.DeliverAsync(orderId, new(Guid.NewGuid(), (await test.Sales.Orders.GetAsync(orderId))!.Version,
            [new(glasses, (await helper.ProWork.GetAsync(glasses))!.Work.Version)]))).Succeeded);
        var extra = Guid.NewGuid(); Assert.True((await helper.Fulfilment.CreateWorkAsync(orderId, new(extra,
            (await test.Sales.Orders.GetAsync(orderId))!.Version, [new(lines[3].Id)]))).Succeeded);
        await helper.ApproveAsync(extra);
        var works = (await helper.Fulfilment.WorksAsync(orderId)).Items;
        var delivery = await helper.Fulfilment.DeliverAsync(orderId, new(Guid.NewGuid(), (await test.Sales.Orders.GetAsync(orderId))!.Version,
            works.Select(x => new DeliveryWorkVersion(x.WorkId, x.Version)).ToArray()));
        Assert.True(delivery.Succeeded, delivery.Error);
        await using var db = await test.Sales.Factory.CreateDbContextAsync();
        Assert.Equal(4, await db.StockDeliveryProofs.CountAsync()); Assert.Equal(4, await db.StockMovements.CountAsync(x => x.Kind == StockMovementKind.Consume));
        Assert.Equal(3, await db.Set<CommercialWorkLine>().CountAsync(x => x.WorkId == glasses));
        Assert.Equal(2, await db.LaboratoryReceipts.CountAsync());
    }

    [Fact]
    public async Task ExplicitReplacementPreservesQualityMaterialsAndIdempotency()
    {
        await using var test = await InventoryScenario.CreateAsync(); var helper = new DeliveryScenario(test);
        var batch = await test.ReceiveAsync(); Assert.True((await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batch))).Succeeded);
        var old = await helper.GroupAsync(); await helper.PayAsync(); await helper.ApproveAsync(old);
        var id = Guid.NewGuid(); var input = new CommercialWorkInput(id, (await helper.Fulfilment.GetAsync(test.OrderId))!.OrderVersion,
            [new(test.LineId)], "Nueva identificación ficticia", old, (await helper.ProWork.GetAsync(old))!.Work.Version, "Reorganización explícita ficticia");
        Assert.False((await helper.Fulfilment.CreateWorkAsync(test.OrderId, input with { SubmissionId = Guid.NewGuid(), FrameReference = new string('X', 201) })).Succeeded);
        Assert.Equal(old, Assert.Single((await helper.Fulfilment.WorksAsync(test.OrderId)).Items).WorkId);
        Assert.Equal(input.PreviousWorkVersion, (await helper.ProWork.GetAsync(old))!.Work.Version);
        Assert.True((await helper.Fulfilment.CreateWorkAsync(test.OrderId, input)).Succeeded);
        Assert.True((await helper.Fulfilment.CreateWorkAsync(test.OrderId, input)).Succeeded);
        Assert.Equal(SalesResultCode.Conflict, (await helper.Fulfilment.CreateWorkAsync(test.OrderId, input with { FrameReference = "Otro" })).Code);
        Assert.Equal(2, (await helper.Fulfilment.WorksAsync(test.OrderId, true)).Items.Count);
        var current = Assert.Single((await helper.Fulfilment.WorksAsync(test.OrderId)).Items);
        Assert.Equal(old, current.PreviousWorkId); Assert.Equal(input.ReplacementReason, current.ReplacementReason);
        await using var db = await test.Sales.Factory.CreateDbContextAsync();
        var previous = await db.Set<CommercialWork>().SingleAsync(x => x.WorkId == old);
        Assert.False(previous.IsCurrent); Assert.NotNull(previous.TechnicalMaterialHash);
        Assert.Equal(input.ReplacementReason, (await db.WorkTransitions.SingleAsync(x => x.Action == "delivery.work.replaced")).Reason);
        Assert.Equal(2, await db.QualityControls.CountAsync(x => x.WorkId == old && x.MaterialHash != null));
        await helper.ApproveAsync(id); Assert.True((await helper.Fulfilment.DeliverAsync(test.OrderId, await helper.DeliveryAsync())).Succeeded);
    }

    [Fact]
    public async Task CrossOrderLinksAndDuplicateSelectionsAreRejectedByServiceAndDatabase()
    {
        await using var test = await InventoryScenario.CreateAsync(); var helper = new DeliveryScenario(test);
        var work = await helper.GroupAsync();
        using var customers = await CustomerScenario.OpenAsync(test.Test.App);
        var otherCustomer = (await customers.Customers.CreateAsync(CustomerScenario.Input("FIC-OTRO-CONJUNTO"))).CustomerId!.Value;
        var another = await test.Sales.ConfirmAsync(await test.Sales.DraftAsync(test.Catalog, otherCustomer, test.VariantId));
        var otherLine = Assert.Single((await test.Sales.Orders.LinesAsync(another))!.Items);
        Assert.False((await helper.Fulfilment.CreateWorkAsync(test.OrderId, new(Guid.NewGuid(), (await helper.Fulfilment.GetAsync(test.OrderId))!.OrderVersion,
            [new(otherLine.Id)]))).Succeeded);
        Assert.False((await helper.Fulfilment.CreateWorkAsync(test.OrderId, new(Guid.NewGuid(), (await helper.Fulfilment.GetAsync(test.OrderId))!.OrderVersion,
            [new(test.LineId), new(test.LineId)]))).Succeeded);
        await using var db = await test.Sales.Factory.CreateDbContextAsync();
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE CommercialWorks SET OrderId = {another} WHERE WorkId = {work}"));
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE CommercialWorkLines SET LineId = {otherLine.Id} WHERE WorkId = {work}"));
        Assert.Equal(test.OrderId, (await db.Set<CommercialWork>().SingleAsync(x => x.WorkId == work)).OrderId);
    }
}
