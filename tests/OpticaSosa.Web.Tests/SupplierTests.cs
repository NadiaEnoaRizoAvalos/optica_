using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Catalog;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class SupplierTests
{
    [Fact]
    public async Task SupplierAndLaboratoryShareOneRecordAndPreserveAccountsWithAtomicAudit()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var scenario = await CatalogScenario.OpenAsync(app);
        var input = new SupplierInput { Name = "Laboratorio ficticio", IsLaboratory = true, TaxId = "FIC-001" };
        var saved = await scenario.Suppliers.CreateAsync(input);
        Assert.True(saved.Succeeded);
        Assert.Equal(saved, await scenario.Suppliers.CreateAsync(input));
        var details = (await scenario.Suppliers.GetAsync(saved.Id!.Value))!;
        Assert.True(details.Supplier.IsLaboratory); Assert.True(details.Supplier.SuppliesProducts);
        Assert.Equal("FIC-001", details.TaxId);
        await using var db = await scenario.Factory.CreateDbContextAsync();
        Assert.Equal(3, await db.Users.CountAsync());
        Assert.Single(await db.Suppliers.ToListAsync());
        var audit = Assert.Single(await db.AuditEntries.Where(x => x.Action == "supplier.created").ToListAsync());
        Assert.Equal(scenario.ActorId, audit.ActorId); Assert.Equal(saved.Id.ToString(), audit.TargetId);
        Assert.DoesNotContain(input.Name, audit.Action);
    }

    [Fact]
    public async Task DuplicateNamesAndFiscalIdsIncludeInactiveRecordsAndConcurrentEditsConflict()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var scenario = await CatalogScenario.OpenAsync(app);
        var input = new SupplierInput { Name = "Proveedor ficticio", TaxId = "FIC-002" };
        var created = await scenario.Suppliers.CreateAsync(input);
        Assert.True(created.Succeeded);
        var original = (await scenario.Suppliers.GetAsync(created.Id!.Value))!;
        input.Version = original.Version; input.IsActive = false;
        Assert.True((await scenario.Suppliers.UpdateAsync(original.Supplier.Id, input)).Succeeded);
        Assert.Equal(CatalogResultCode.Conflict, (await scenario.Suppliers.UpdateAsync(original.Supplier.Id, input)).Code);
        Assert.Empty((await scenario.Suppliers.SearchAsync(null)).Items);
        Assert.Single((await scenario.Suppliers.SearchAsync(null, includeInactive: true)).Items);
        Assert.Equal(CatalogResultCode.Duplicate, (await scenario.Suppliers.CreateAsync(new() { Name = "  proveedor   FICTICIO " })).Code);
        Assert.Equal(CatalogResultCode.Duplicate, (await scenario.Suppliers.CreateAsync(new() { Name = "Otro ficticio", TaxId = "fic 002" })).Code);
        input.IsActive = true;
        Assert.Equal(CatalogResultCode.Conflict, (await scenario.Suppliers.CreateAsync(input)).Code);
    }

    [Theory]
    [InlineData("ventas.ficticio", true)]
    [InlineData("profesional.ficticio", false)]
    public async Task SupplierPermissionsAreEnforcedByApplicationNotOnlyByScreens(string user, bool mayRead)
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var scenario = await CatalogScenario.OpenAsync(app, user);
        if (mayRead)
        {
            Assert.Empty((await scenario.Suppliers.SearchAsync(null)).Items);
            Assert.Null(await scenario.Suppliers.GetAsync(Guid.NewGuid()));
        }
        else
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scenario.Suppliers.SearchAsync(null));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scenario.Suppliers.GetAsync(Guid.NewGuid()));
        }
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scenario.Suppliers.CreateAsync(new() { Name = "Ficticio no autorizado" }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scenario.Suppliers.UpdateAsync(Guid.NewGuid(), new()));
        await using var db = await scenario.Factory.CreateDbContextAsync();
        Assert.Empty(await db.Suppliers.ToListAsync());
    }

    [Fact]
    public async Task HistoriesArePaginatedAndLaboratoriesFilterDoesNotCreateAccounts()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var scenario = await CatalogScenario.OpenAsync(app);
        for (var i = 0; i < 53; i++)
            Assert.True((await scenario.Suppliers.CreateAsync(new() { Name = $"Proveedor ficticio {i:D3}", IsLaboratory = i % 2 == 0 })).Succeeded);
        var first = await scenario.Suppliers.SearchAsync(null);
        var second = await scenario.Suppliers.SearchAsync(null, page: 2);
        Assert.Equal(50, first.Items.Count); Assert.True(first.HasMore);
        Assert.Equal(3, second.Items.Count); Assert.False(second.HasMore);
        Assert.Empty(first.Items.Select(x => x.Id).Intersect(second.Items.Select(x => x.Id)));
        var labs = await scenario.Suppliers.SearchAsync(null, laboratoriesOnly: true);
        Assert.Equal(27, labs.Items.Count); Assert.All(labs.Items, x => Assert.True(x.IsLaboratory));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => scenario.Suppliers.SearchAsync(null, page: 0));
    }

    [Fact]
    public async Task ConcurrentDuplicateSubmissionsCommitExactlyOneSupplierAndOneAudit()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var a = await CatalogScenario.OpenAsync(app);
        using var b = await CatalogScenario.OpenAsync(app);
        var results = await Task.WhenAll(a.Suppliers.CreateAsync(new() { Name = "Concurrente ficticio" }),
            b.Suppliers.CreateAsync(new() { Name = "Concurrente ficticio" }));
        Assert.Single(results, x => x.Succeeded);
        await using var db = await a.Factory.CreateDbContextAsync();
        Assert.Single(await db.Suppliers.ToListAsync());
        Assert.Single(await db.AuditEntries.Where(x => x.Action == "supplier.created").ToListAsync());
    }
}
