using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Commerce;
using OpticaSosa.Application.Catalog;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Commerce;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class SalesOrderTests
{
    internal static async Task<(Guid Product, Guid Variant)> VariantAsync(CatalogScenario catalog, ProductCategory category = ProductCategory.Frame)
    {
        var product = await CatalogTests.ProductAsync(catalog, category);
        var input = await CatalogTests.InputAsync(catalog, product);
        if (category == ProductCategory.Lens) input.Lens = new("Material ficticio", 1.6m, "Tratamiento ficticio");
        if (category == ProductCategory.ContactLens) { input.UnitsPerPackage = 6; input.Contact = new(1.125m, 0, null, null, 8.6m, 14.2m); }
        var variant = await catalog.Catalog.CreateVariantAsync(product, input);
        Assert.True(variant.Succeeded, variant.Error); return (product, variant.Id!.Value);
    }
    [Fact]
    public async Task DraftEditsPreserveEarlierLinesAndConfirmedOrdersKeepTheirAgreedPrices()
    {
        await using var test = await TechnicalTestContext.CreateAsync(); using var catalog = await CatalogScenario.OpenAsync(test.App);
        using var sales = await CommerceScenario.OpenAsync(test.App, "ventas.ficticio");
        var item = await VariantAsync(catalog);
        var input = await sales.DraftAsync(catalog, test.CustomerId, item.Variant);
        input.Lines = [input.Lines[0] with { Quantity = 2, UnitPrice = 12.345m, Discount = 1m, DiscountReason = "Prueba ficticia" }];
        var created = await sales.Orders.CreateDraftAsync(input); Assert.True(created.Succeeded, created.Error);
        Assert.Equal(created, await sales.Orders.CreateDraftAsync(input));
        var before = (await sales.Orders.GetAsync(created.OrderId!.Value))!;
        Assert.Equal(2370, before.TotalMinor);
        var edit = await sales.DraftAsync(catalog, test.CustomerId, item.Variant, 50m);
        edit.Version = before.Version;
        Assert.True((await sales.Orders.UpdateDraftAsync(before.Id, edit)).Succeeded);
        Assert.Equal(1235, Assert.Single((await sales.Orders.LinesAsync(before.Id, draftNumber: 1))!.Items).UnitPriceMinor);
        var current = (await sales.Orders.GetAsync(before.Id))!;
        Assert.Equal(2, current.DraftNumber); Assert.Equal(5000, current.TotalMinor);
        Assert.True((await sales.Orders.ConfirmAsync(before.Id, new(Guid.NewGuid(), current.Version))).Succeeded);
        edit = await sales.DraftAsync(catalog, test.CustomerId, item.Variant, 60m);
        edit.Version = (await sales.Orders.GetAsync(before.Id))!.Version;
        Assert.Equal(SalesResultCode.Invalid, (await sales.Orders.UpdateDraftAsync(before.Id, edit)).Code);
        var variant = (await catalog.Catalog.VariantAsync(item.Product, item.Variant))!;
        var repricing = await CatalogTests.InputAsync(catalog, item.Product);
        repricing.Version = variant.Technical.Version; repricing.SalePrice = 999;
        Assert.True((await catalog.Catalog.UpdateVariantAsync(item.Product, item.Variant, repricing)).Succeeded);
        Assert.Equal(5000, (await sales.Orders.GetAsync(before.Id))!.TotalMinor);
        Assert.Equal(5000, Assert.Single((await sales.Orders.LinesAsync(before.Id))!.Items).UnitPriceMinor);
        Assert.Equal(1.125m, (await test.Clinical.Clinical.GetAsync(test.CustomerId, test.RecordId))!.Data.Right.Sphere);
    }
    [Theory]
    [InlineData(ProductCategory.Lens)]
    [InlineData(ProductCategory.ContactLens)]
    public async Task TechnicalPendingDoesNotBlockConfirmationOrDeposit(ProductCategory category)
    {
        await using var test = await TechnicalTestContext.CreateAsync(); using var catalog = await CatalogScenario.OpenAsync(test.App);
        using var sales = await CommerceScenario.OpenAsync(test.App, "ventas.ficticio");
        var item = await VariantAsync(catalog, category);
        var id = await sales.ConfirmAsync(await sales.DraftAsync(catalog, test.CustomerId, item.Variant));
        var order = (await sales.Orders.GetAsync(id))!;
        Assert.True((await sales.Payments.ReceiveAsync(id, new(Guid.NewGuid(), order.Version, 25m, "ARS", "Efectivo ficticio"))).Succeeded);
        Assert.Equal(7500, (await sales.Orders.GetAsync(id))!.BalanceMinor);
        await using var db = await sales.Factory.CreateDbContextAsync();
        Assert.Empty(await db.TechnicalPublications.ToListAsync()); Assert.Empty(await db.OpticalWorks.ToListAsync());
    }
    [Fact]
    public async Task DraftValidationAndFailedCommandsDoNotLeavePartialOrdersOrAudit()
    {
        await using var test = await TechnicalTestContext.CreateAsync(); using var catalog = await CatalogScenario.OpenAsync(test.App);
        using var sales = await CommerceScenario.OpenAsync(test.App);
        var item = await VariantAsync(catalog);
        var invalid = await sales.DraftAsync(catalog, test.CustomerId, item.Variant);
        invalid.Lines = [invalid.Lines[0] with { Discount = 101, DiscountReason = "Ficticio" }];
        Assert.Equal(SalesResultCode.Invalid, (await sales.Orders.CreateDraftAsync(invalid)).Code);
        await using var db = await sales.Factory.CreateDbContextAsync();
        Assert.Empty(await db.SalesOrders.ToListAsync()); Assert.Empty(await db.OrderCommandReceipts.ToListAsync());
        Assert.Empty(await db.AuditEntries.Where(x => x.Action.StartsWith("sales.")).ToListAsync());
        invalid = await sales.DraftAsync(catalog, test.CustomerId, item.Variant); invalid.Currency = "USD";
        Assert.Equal(SalesResultCode.Invalid, (await sales.Orders.CreateDraftAsync(invalid)).Code);
        invalid = await sales.DraftAsync(catalog, test.CustomerId, item.Variant);
        invalid.Lines = [invalid.Lines[0] with { VariantVersion = Guid.NewGuid() }];
        Assert.Equal(SalesResultCode.Conflict, (await sales.Orders.CreateDraftAsync(invalid)).Code);
        var empty = new SalesDraftInput { CustomerId = test.CustomerId, Currency = "ARS" };
        var created = await sales.Orders.CreateDraftAsync(empty); Assert.True(created.Succeeded);
        var draft = (await sales.Orders.GetAsync(created.OrderId!.Value))!;
        Assert.Equal(SalesResultCode.Invalid, (await sales.Orders.ConfirmAsync(draft.Id, new(Guid.NewGuid(), draft.Version))).Code);
    }
}
