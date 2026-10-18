using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Commerce;
using OpticaSosa.Domain.Commerce;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class SalesPermissionTests
{
    [Fact]
    public async Task ProfessionalWithoutSalesRoleCannotReadOrChangeFinancialInformation()
    {
        await using var test = await TechnicalTestContext.CreateAsync();
        using var professional = await CommerceScenario.OpenAsync(test.App, "profesional.ficticio");
        var id = Guid.NewGuid();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => professional.Orders.ListAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => professional.Orders.GetAsync(id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => professional.Orders.LinesAsync(id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => professional.Orders.CreateDraftAsync(new()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => professional.Orders.ConfirmAsync(id, new(Guid.NewGuid(), Guid.NewGuid())));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => professional.Orders.CancelAsync(id, new(Guid.NewGuid(), Guid.NewGuid(), "Ficticio")));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => professional.Payments.ListAsync(id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => professional.Payments.ReceiveAsync(id, new(Guid.NewGuid(), Guid.NewGuid(), 10, "ARS", "Ficticio")));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => professional.Payments.RefundAsync(id, new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10, "ARS", "Ficticio", "Ficticio")));
        Assert.NotNull(await test.Clinical.Clinical.GetAsync(test.CustomerId, test.RecordId));
    }
    [Fact]
    public async Task UnpaidCancellationIsAvailableToSalesAndDoesNotCreateARefund()
    {
        await using var test = await TechnicalTestContext.CreateAsync(); using var catalog = await CatalogScenario.OpenAsync(test.App);
        using var sales = await CommerceScenario.OpenAsync(test.App, "ventas.ficticio");
        var item = await SalesOrderTests.VariantAsync(catalog);
        var id = await sales.ConfirmAsync(await sales.DraftAsync(catalog, test.CustomerId, item.Variant));
        var order = (await sales.Orders.GetAsync(id))!;
        Assert.True((await sales.Orders.CancelAsync(id, new(Guid.NewGuid(), order.Version, "Ficticio sin compra"))).Succeeded);
        Assert.Equal(SalesOrderStatus.Cancelled, (await sales.Orders.GetAsync(id))!.Status);
        Assert.Empty((await sales.Payments.ListAsync(id))!.Items);
        await using var db = await sales.Factory.CreateDbContextAsync();
        Assert.Single(await db.ClinicalRecords.ToListAsync()); Assert.Empty(await db.OpticalWorks.ToListAsync());
    }
    [Fact]
    public async Task LongDraftsAndOrderListsUsePaginationWithoutLosingHistory()
    {
        await using var test = await TechnicalTestContext.CreateAsync(); using var catalog = await CatalogScenario.OpenAsync(test.App);
        using var sales = await CommerceScenario.OpenAsync(test.App);
        var item = await SalesOrderTests.VariantAsync(catalog);
        var input = await sales.DraftAsync(catalog, test.CustomerId, item.Variant);
        input.Lines = Enumerable.Range(0, 52).Select(_ => input.Lines[0]).ToArray();
        var created = await sales.Orders.CreateDraftAsync(input); Assert.True(created.Succeeded, created.Error);
        var first = (await sales.Orders.LinesAsync(created.OrderId!.Value))!;
        var second = (await sales.Orders.LinesAsync(created.OrderId.Value, page: 2))!;
        Assert.Equal(50, first.Items.Count); Assert.True(first.HasMore); Assert.Equal(2, second.Items.Count); Assert.False(second.HasMore);
        Assert.Empty(first.Items.Select(x => x.Id).Intersect(second.Items.Select(x => x.Id)));
        Assert.Single((await sales.Orders.ListAsync(test.CustomerId)).Items);
        Assert.Empty((await sales.Orders.ListAsync(Guid.NewGuid())).Items);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sales.Orders.ListAsync(page: 0));
    }
}
