using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Inventory;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Commerce;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Infrastructure.Inventory;
using Xunit;

namespace OpticaSosa.Web.Tests;

internal sealed class InventoryScenario : IAsyncDisposable
{
    public TechnicalTestContext Test { get; }
    public CatalogScenario Catalog { get; }
    public CommerceScenario Sales { get; }
    public StockService Stock { get; }
    public OrderTechnicalService Technical { get; }
    public MutableInventoryClock Clock { get; } = new();
    public Guid ProductId { get; }
    public Guid VariantId { get; }
    public Guid OrderId { get; }
    public Guid LineId { get; }
    private InventoryScenario(TechnicalTestContext test, CatalogScenario catalog, CommerceScenario sales, Guid product, Guid variant, Guid order, Guid line)
    {
        Test = test; Catalog = catalog; Sales = sales; ProductId = product; VariantId = variant; OrderId = order; LineId = line;
        Stock = new(sales.Factory, sales.Actor, Clock); Technical = new(sales.Factory, test.Clinical.Actor, Clock);
    }
    public static async Task<InventoryScenario> CreateAsync(ProductCategory category = ProductCategory.Frame, int quantity = 1, FulfilmentSource source = FulfilmentSource.Stock)
    {
        var test = await TechnicalTestContext.CreateAsync(); var catalog = await CatalogScenario.OpenAsync(test.App);
        var sales = await CommerceScenario.OpenAsync(test.App, "ventas.ficticio");
        var item = await SalesOrderTests.VariantAsync(catalog, category);
        if (category == ProductCategory.Lens && source == FulfilmentSource.Stock)
        {
            var current = (await catalog.Catalog.VariantAsync(item.Product, item.Variant))!;
            var input = await CatalogTests.InputAsync(catalog, item.Product);
            var eye = test.Source.Right;
            input.Version = current.Technical.Version;
            input.Lens = new("Material ficticio", 1.6m, "Tratamiento ficticio", eye.Sphere, eye.Cylinder, eye.Axis, eye.Addition);
            Assert.True((await catalog.Catalog.UpdateVariantAsync(item.Product, item.Variant, input)).Succeeded);
        }
        var draft = await sales.DraftAsync(catalog, test.CustomerId, item.Variant, quantity: quantity);
        draft.Lines = [draft.Lines[0] with { Source = source }];
        var order = await sales.ConfirmAsync(draft);
        var line = Assert.Single((await sales.Orders.LinesAsync(order))!.Items);
        return new(test, catalog, sales, item.Product, item.Variant, order, line.Id);
    }
    public async Task<StockReceiptInput> ReceiptAsync(int quantity = 1, int quarantine = 0, string? lot = null, DateOnly? expiry = null, ExpiryPrecision precision = ExpiryPrecision.None)
    {
        await using var db = await Sales.Factory.CreateDbContextAsync();
        var variant = await db.ProductVariants.AsNoTracking().SingleAsync(x => x.Id == VariantId);
        return new(Guid.NewGuid(), VariantId, variant.Version, quantity, quarantine, lot, expiry, precision, "Entrada ficticia de prueba");
    }
    public async Task<Guid> ReceiveAsync(int quantity = 1, int quarantine = 0, string? lot = null, DateOnly? expiry = null, ExpiryPrecision precision = ExpiryPrecision.None)
    {
        var result = await Stock.ReceiveAsync(await ReceiptAsync(quantity, quarantine, lot, expiry, precision));
        Assert.True(result.Succeeded, result.Error); return result.BatchId!.Value;
    }
    public async Task<Guid> AssignAsync(TechnicalEyes eyes = TechnicalEyes.Right)
    {
        var input = await Test.InputAsync();
        if ((await Catalog.Catalog.GetAsync(ProductId))!.Product.Category == ProductCategory.ContactLens)
        {
            input.Purpose = OpticaSosa.Domain.Technical.TechnicalPurpose.ContactLenses; input.Mounting = null;
            input.RightContact = new(VariantId, 8.6m, 14.2m);
        }
        var publication = await Test.Publications.PublishAsync(Test.CustomerId, Test.RecordId, input);
        Assert.True(publication.Succeeded, publication.Error);
        var p = (await Test.Publications.GetAsync(Test.CustomerId, publication.Id!.Value))!;
        var order = (await Sales.Orders.GetAsync(OrderId))!;
        var assignment = Guid.NewGuid();
        var assigned = await Technical.AssignAsync(OrderId, LineId, new(assignment, order.Version, p.Id, p.Version, eyes));
        Assert.True(assigned.Succeeded, assigned.Error); return assignment;
    }
    public async Task<ReserveStockInput> ReserveInputAsync(Guid batchId, int quantity = 1, Guid? assignment = null)
    {
        var batch = (await Stock.BatchesAsync(VariantId)).Items.Single(x => x.Id == batchId);
        var order = (await Sales.Orders.GetAsync(OrderId))!;
        return new(Guid.NewGuid(), order.Version, batch.Id, batch.Version, assignment, quantity);
    }
    public async ValueTask DisposeAsync() { Sales.Dispose(); Catalog.Dispose(); await Test.DisposeAsync(); }
    internal sealed class MutableInventoryClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
        public TimeZoneInfo Zone { get; set; } = TimeZoneInfo.Utc;
        public override TimeZoneInfo LocalTimeZone => Zone;
    }
}
