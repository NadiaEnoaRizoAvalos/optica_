using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Commerce;
using OpticaSosa.Application.Delivery;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Commerce;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Infrastructure.Inventory;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class OrderDeliveryLabTests
{
    [Fact]
    public async Task PartialLaboratoryReceiptsAndQuarantineCannotBePreparedUntilAllMaterialIsReviewedAndReserved()
    {
        await using var lab = await LaboratoryScenario.CreateAsync(quantity: 2); var test = lab.Inventory; var delivery = new DeliveryScenario(test);
        var work = await delivery.GroupAsync(lab.AssignmentId); var encargo = await lab.AuthorizeAsync(2); await lab.DispatchAsync(encargo);
        Assert.False((await delivery.ProWork.TransitionAsync(work, (await delivery.ProWork.GetAsync(work))!.Work.Version, "prepare")).Succeeded);
        Assert.True((await lab.Laboratory.ReceiveAsync(test.OrderId, encargo, await lab.ReceiptAsync(encargo))).Succeeded);
        var first = Assert.Single((await test.Stock.BatchesAsync(test.VariantId)).Items);
        Assert.True((await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(first.Id, assignment: lab.AssignmentId))).Succeeded);
        Assert.False((await delivery.ProWork.TransitionAsync(work, (await delivery.ProWork.GetAsync(work))!.Work.Version, "prepare")).Succeeded);
        Assert.True((await lab.Laboratory.ReceiveAsync(test.OrderId, encargo, await lab.ReceiptAsync(encargo, 1, 1))).Succeeded);
        var observed = Assert.Single((await test.Stock.BatchesAsync(test.VariantId)).Items, x => x.Quarantined == 1);
        Assert.False((await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(observed.Id, assignment: lab.AssignmentId))).Succeeded);
        var review = new InventoryAdministration(test.Sales.Factory, test.Test.Clinical.Actor, test.Clock);
        var hold = Assert.Single((await review.HoldsAsync(observed.Id)).Items);
        Assert.True((await review.ReviewAsync(observed.Id, hold.Id, new(Guid.NewGuid(), observed.Version, hold.Version, 1,
            true, true, true, null, "Material ficticio verificado"))).Succeeded);
        Assert.True((await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(observed.Id, assignment: lab.AssignmentId))).Succeeded);
        await delivery.PayAsync(); await delivery.ApproveAsync(work);
        Assert.True((await delivery.Fulfilment.DeliverAsync(test.OrderId, await delivery.DeliveryAsync())).Succeeded);
        Assert.All((await test.Stock.BatchesAsync(test.VariantId)).Items, x => Assert.Equal(0, x.OnHand));
    }

    [Fact]
    public async Task ManyIndependentWorksArePagedWithoutAFixedMaximum()
    {
        await using var test = await InventoryScenario.CreateAsync(ProductCategory.Accessory); var delivery = new DeliveryScenario(test);
        var draft = await test.Sales.DraftAsync(test.Catalog, test.Test.CustomerId, test.VariantId);
        draft.Lines = Enumerable.Range(0, 51).Select(_ => draft.Lines[0]).ToArray();
        var order = await test.Sales.ConfirmAsync(draft);
        var first = await delivery.Fulfilment.MaterialsAsync(order); var second = await delivery.Fulfilment.MaterialsAsync(order, 2);
        Assert.Equal(50, first.Items.Count); Assert.True(first.HasMore); Assert.Single(second.Items);
        foreach (var line in first.Items.Concat(second.Items))
        {
            var created = await delivery.Fulfilment.CreateWorkAsync(order, new(Guid.NewGuid(), (await delivery.Fulfilment.GetAsync(order))!.OrderVersion, [new(line.LineId)]));
            Assert.True(created.Succeeded, created.Error);
        }
        var works = await delivery.Fulfilment.WorksAsync(order); var more = await delivery.Fulfilment.WorksAsync(order, page: 2);
        Assert.Equal(50, works.Items.Count); Assert.True(works.HasMore); Assert.Single(more.Items);
        Assert.Empty(works.Items.Select(x => x.WorkId).Intersect(more.Items.Select(x => x.WorkId)));
    }
}
