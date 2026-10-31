using OpticaSosa.Application.Delivery;
using OpticaSosa.Domain.Work;
using OpticaSosa.Infrastructure.Delivery;
using OpticaSosa.Infrastructure.Work;
using Xunit;

namespace OpticaSosa.Web.Tests;

internal sealed class DeliveryScenario(InventoryScenario inventory)
{
    public InventoryScenario Inventory { get; } = inventory;
    public OrderFulfilmentService Fulfilment { get; } = new(inventory.Sales.Factory, inventory.Sales.Actor, inventory.Clock);
    public WorkService SalesWork { get; } = new(inventory.Sales.Factory, inventory.Sales.Actor, inventory.Clock);
    public WorkService ProWork { get; } = new(inventory.Sales.Factory, inventory.Test.Clinical.Actor, inventory.Clock);
    public async Task<Guid> GroupAsync(Guid? assignment = null)
    {
        var input = new CommercialWorkInput(Guid.NewGuid(), (await Fulfilment.GetAsync(Inventory.OrderId))!.OrderVersion,
            [new(Inventory.LineId, assignment)], "Armazón ficticio aportado");
        var result = await Fulfilment.CreateWorkAsync(Inventory.OrderId, input); Assert.True(result.Succeeded, result.Error);
        return input.SubmissionId;
    }
    public async Task PayAsync()
    {
        var order = (await Inventory.Sales.Orders.GetAsync(Inventory.OrderId))!;
        if (order.BalanceMinor > 0)
            Assert.True((await Inventory.Sales.Payments.ReceiveAsync(order.Id, new(Guid.NewGuid(), order.Version,
                order.BalanceMinor / 100m, order.Currency, "Medio ficticio"))).Succeeded);
    }
    public async Task ApproveAsync(Guid workId)
    {
        var w = (await ProWork.GetAsync(workId))!.Work;
        if (w.Status == WorkStatus.Registered) await QualityWorkflowTests.PrepareAsync(ProWork, workId);
        foreach (var stage in Enum.GetValues<QualityStage>())
        {
            var current = (await ProWork.GetAsync(workId))!.Work;
            if (stage == QualityStage.Technical ? current.TechnicalApproved : current.DeliveryApproved) continue;
            var input = await QualityWorkflowTests.InputAsync(ProWork, workId, stage);
            var result = await ProWork.SaveControlAsync(workId, input); Assert.True(result.Succeeded, result.Error);
        }
    }
    public async Task<DeliverOrderInput> DeliveryAsync() => new(Guid.NewGuid(), (await Fulfilment.GetAsync(Inventory.OrderId))!.OrderVersion,
        (await Fulfilment.WorksAsync(Inventory.OrderId)).Items.Select(x => new DeliveryWorkVersion(x.WorkId, x.Version)).ToArray());
}
