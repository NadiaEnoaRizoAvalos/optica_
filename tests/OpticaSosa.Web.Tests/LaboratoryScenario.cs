using OpticaSosa.Application.Laboratory;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Commerce;
using OpticaSosa.Infrastructure.Laboratory;
using Xunit;

namespace OpticaSosa.Web.Tests;

internal sealed class LaboratoryScenario : IAsyncDisposable
{
    public InventoryScenario Inventory { get; }
    public LaboratoryService Laboratory { get; }
    public Guid SupplierId { get; }
    public Guid? AssignmentId { get; }
    private LaboratoryScenario(InventoryScenario inventory, Guid supplier, Guid? assignment)
    {
        Inventory = inventory; SupplierId = supplier; AssignmentId = assignment;
        Laboratory = new(inventory.Sales.Factory, inventory.Sales.Actor, inventory.Clock);
    }
    public static async Task<LaboratoryScenario> CreateAsync(int quantity = 3, bool assign = true, ProductCategory category = ProductCategory.Lens)
    {
        var inventory = await InventoryScenario.CreateAsync(category, quantity, FulfilmentSource.Laboratory);
        var supplier = await inventory.Catalog.Suppliers.CreateAsync(new() { Name = "Laboratorio ficticio", IsLaboratory = true });
        Assert.True(supplier.Succeeded);
        var assignment = assign && category is ProductCategory.Lens or ProductCategory.ContactLens ? await inventory.AssignAsync() : (Guid?)null;
        return new(inventory, supplier.Id!.Value, assignment);
    }
    public async Task<LaboratoryCreateInput> CreateInputAsync(int quantity = 3) => new(Guid.NewGuid(),
        (await Inventory.Sales.Orders.GetAsync(Inventory.OrderId))!.Version, SupplierId,
        (await Inventory.Catalog.Suppliers.GetAsync(SupplierId))!.Version, AssignmentId, quantity, new(2026, 10, 20), true);
    public async Task<Guid> AuthorizeAsync(int quantity = 3)
    {
        var input = await CreateInputAsync(quantity);
        var result = await Laboratory.CreateAsync(Inventory.OrderId, Inventory.LineId, input);
        Assert.True(result.Succeeded, result.Error); return input.SubmissionId;
    }
    public async Task DispatchAsync(Guid labId)
    {
        var lab = (await Laboratory.GetAsync(Inventory.OrderId, labId))!;
        var result = await Laboratory.RecordDispatchAsync(Inventory.OrderId, labId, new(Guid.NewGuid(), lab.OrderVersion, lab.Version, "Envío ficticio confirmado"));
        Assert.True(result.Succeeded, result.Error);
    }
    public async Task<LaboratoryReceiveInput> ReceiptAsync(Guid labId, int quantity = 1, int quarantine = 0)
    {
        var lab = (await Laboratory.GetAsync(Inventory.OrderId, labId))!;
        return new(Guid.NewGuid(), lab.OrderVersion, lab.Version, quantity, quarantine, null, null,
            OpticaSosa.Domain.Inventory.ExpiryPrecision.None, "Resultado ficticio de recepción", "Remito ficticio");
    }
    public ValueTask DisposeAsync() => Inventory.DisposeAsync();
}
