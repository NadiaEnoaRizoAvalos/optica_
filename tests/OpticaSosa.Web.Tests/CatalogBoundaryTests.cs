using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Application.Catalog;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Infrastructure.Persistence;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class CatalogBoundaryTests
{
    [Fact]
    public async Task SqliteEnforcesVariantCategoryMoneyAndPackagingEvenOutsideApplicationServices()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync(); using var scenario = await CatalogScenario.OpenAsync(app);
        var product = await CatalogTests.ProductAsync(scenario, ProductCategory.Lens);
        var input = await CatalogTests.InputAsync(scenario, product); var created = await scenario.Catalog.CreateVariantAsync(product, input);
        Assert.True(created.Succeeded);
        await using var db = await scenario.Factory.CreateDbContextAsync();
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE ProductVariants SET Category=4 WHERE Id={created.Id}"));
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE ProductVariants SET PriceMinor=1.25 WHERE Id={created.Id}"));
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE ProductVariants SET PriceCurrency=NULL WHERE Id={created.Id}"));
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE ProductVariants SET UnitsPerPackage=2 WHERE Id={created.Id}"));
        Assert.Equal(1235, (await scenario.Catalog.VariantAsync(product, created.Id!.Value))!.SalePrice!.MinorUnits);
    }

    [Fact]
    public async Task VariantPaginationHasNoFixedCatalogMaximumAndInactiveProductsDisappearFromTechnicalQueries()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync(); using var scenario = await CatalogScenario.OpenAsync(app);
        var product = await CatalogTests.ProductAsync(scenario);
        for (var i = 0; i < 52; i++)
        {
            var input = await CatalogTests.InputAsync(scenario, product, $"SKU-FIC-{i:D3}");
            Assert.True((await scenario.Catalog.CreateVariantAsync(product, input)).Succeeded);
        }
        var first = await scenario.Catalog.VariantsAsync(product); var second = await scenario.Catalog.VariantsAsync(product, page: 2);
        Assert.Equal(50, first.Items.Count); Assert.True(first.HasMore); Assert.Equal(2, second.Items.Count); Assert.False(second.HasMore);
        Assert.Empty(first.Items.Select(x => x.Technical.Id).Intersect(second.Items.Select(x => x.Technical.Id)));
        var original = (await scenario.Catalog.GetAsync(product))!;
        Assert.True((await scenario.Catalog.UpdateAsync(product, new() { Name = original.Product.Name, Category = ProductCategory.Frame,
            Version = original.Product.Version, IsActive = false })).Succeeded);
        Assert.Empty((await scenario.Technical.SearchAsync(null)).Items);
        Assert.Empty((await scenario.Technical.VariantsAsync(product)).Items);
        Assert.Null(await scenario.Technical.VariantAsync(product, first.Items[0].Technical.Id));
        await using var db = await scenario.Factory.CreateDbContextAsync();
        Assert.Equal(52, await db.ProductVariants.CountAsync());
    }

    [Fact]
    public async Task InactiveSuppliersKeepExistingReferencesButCannotBeAddedToNewProducts()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync(); using var scenario = await CatalogScenario.OpenAsync(app);
        var supplier = (await scenario.Suppliers.CreateAsync(new() { Name = "Proveedor ficticio" })).Id!.Value;
        var created = await scenario.Catalog.CreateAsync(new() { Name = "Producto ficticio", Category = ProductCategory.Frame, SupplierIds = [supplier] });
        Assert.True(created.Succeeded);
        var prior = (await scenario.Suppliers.GetAsync(supplier))!;
        Assert.True((await scenario.Suppliers.UpdateAsync(supplier, new() { Name = prior.Supplier.Name, Version = prior.Version, IsActive = false })).Succeeded);
        Assert.False(Assert.Single((await scenario.Catalog.GetAsync(created.Id!.Value))!.Suppliers).IsActive);
        Assert.Equal(CatalogResultCode.Invalid, (await scenario.Catalog.CreateAsync(new() { Name = "Otro ficticio", Category = ProductCategory.Frame, SupplierIds = [supplier] })).Code);
    }

    [Fact]
    public async Task ExistingSnapshotMechanismRestoresCatalogAndCurrencyWithoutChangingSource()
    {
        await using var source = new SecurityApp(); await source.InitializeAsync(); using var scenario = await CatalogScenario.OpenAsync(source);
        var supplier = (await scenario.Suppliers.CreateAsync(new() { Name = "Proveedor de recuperación ficticio" })).Id!.Value;
        var created = await scenario.Catalog.CreateAsync(new() { Name = "Producto de recuperación ficticio", Category = ProductCategory.Frame, SupplierIds = [supplier] });
        Assert.True(created.Succeeded); var product = created.Id!.Value;
        var variant = await scenario.Catalog.CreateVariantAsync(product, await CatalogTests.InputAsync(scenario, product)); Assert.True(variant.Succeeded);
        using var admin = await ClinicalScenario.OpenAsync(source, "admin.ficticio");
        var snapshot = await admin.Snapshots.CreateAsync(); Assert.Equal("Complete", snapshot.Status);
        await using var restored = new SecurityApp();
        using (var scope = restored.Services.CreateScope())
        {
            var paths = scope.ServiceProvider.GetRequiredService<StoragePaths>();
            Assert.Equal(Path.Combine(restored.Root, "private"), paths.DataDirectory);
            File.Copy(Path.Combine(snapshot.Directory, "optica.db"), paths.DatabaseFile, overwrite: false);
        }
        using var recovered = await CatalogScenario.OpenAsync(restored);
        Assert.Equal(1235, (await recovered.Catalog.VariantAsync(product, variant.Id!.Value))!.SalePrice!.MinorUnits);
        Assert.Equal("ARS", (await recovered.Catalog.ConfigurationAsync()).Currency);
        Assert.Equal(supplier, Assert.Single((await recovered.Catalog.GetAsync(product))!.Suppliers).Id);
        Assert.Equal(1235, (await scenario.Catalog.VariantAsync(product, variant.Id.Value))!.SalePrice!.MinorUnits);
    }
}
