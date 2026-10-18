using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Catalog;
using OpticaSosa.Domain.Catalog;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class CatalogTests
{
    internal static async Task<Guid> ProductAsync(CatalogScenario scenario, ProductCategory category = ProductCategory.Frame, string name = "Producto ficticio")
    {
        var result = await scenario.Catalog.CreateAsync(new() { Name = name, Category = category });
        Assert.True(result.Succeeded, result.Error); return result.Id!.Value;
    }
    internal static async Task<VariantInput> InputAsync(CatalogScenario scenario, Guid product, string code = "SKU-FIC") => new()
    { Code = code, ProductVersion = (await scenario.Catalog.GetAsync(product))!.Product.Version, SalePrice = 12.345m, Currency = "ARS" };

    [Fact]
    public async Task ProductAssociatesSeveralSuppliersWithoutDuplicatingArticlesOrGeneratingVariants()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync(); using var scenario = await CatalogScenario.OpenAsync(app);
        var a = (await scenario.Suppliers.CreateAsync(new() { Name = "Proveedor ficticio A" })).Id!.Value;
        var b = (await scenario.Suppliers.CreateAsync(new() { Name = "Laboratorio ficticio B", IsLaboratory = true })).Id!.Value;
        var input = new ProductInput { Name = "Armazón ficticio", Category = ProductCategory.Frame, SupplierIds = [a, b] };
        var created = await scenario.Catalog.CreateAsync(input); Assert.True(created.Succeeded);
        Assert.Equal(created, await scenario.Catalog.CreateAsync(input));
        Assert.Equal(2, (await scenario.Catalog.GetAsync(created.Id!.Value))!.Suppliers.Count);
        Assert.Empty((await scenario.Catalog.VariantsAsync(created.Id.Value)).Items);
        Assert.Equal(CatalogResultCode.Duplicate, (await scenario.Catalog.CreateAsync(new() { Name = " armazón   FICTICIO ", Category = ProductCategory.Frame, SupplierIds = [a] })).Code);
        await using var db = await scenario.Factory.CreateDbContextAsync();
        Assert.Single(await db.Products.ToListAsync()); Assert.Equal(3, await db.Users.CountAsync());
        Assert.Single(await db.AuditEntries.Where(x => x.Action == "product.created").ToListAsync());
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Suppliers WHERE Id={a}"));
    }

    [Theory]
    [InlineData(ProductCategory.Frame)]
    [InlineData(ProductCategory.Lens)]
    [InlineData(ProductCategory.ContactLens)]
    [InlineData(ProductCategory.Accessory)]
    public async Task TypedVariantRoundTripsExactlyAndDoesNotGenerateCombinations(ProductCategory category)
    {
        await using var app = new SecurityApp(); await app.InitializeAsync(); using var scenario = await CatalogScenario.OpenAsync(app);
        var product = await ProductAsync(scenario, category); var input = await InputAsync(scenario, product);
        if (category == ProductCategory.Frame) input.Frame = new("Azul ficticio", 52.50m, 18, 140);
        if (category == ProductCategory.Lens) input.Lens = new("Material ficticio", 1.600m, "Tratamiento ficticio", 1.125m, 0m, 180);
        if (category == ProductCategory.ContactLens) { input.UnitsPerPackage = 6; input.Contact = new(1.125m, 0m, 180, null, 8.60m, 14.20m); }
        var saved = await scenario.Catalog.CreateVariantAsync(product, input); Assert.True(saved.Succeeded, saved.Error);
        Assert.Equal(saved, await scenario.Catalog.CreateVariantAsync(product, input));
        var variant = (await scenario.Catalog.VariantAsync(product, saved.Id!.Value))!;
        Assert.Equal(input.ToSpecification(category), variant.Technical.Specification);
        Assert.Equal(1235, variant.SalePrice!.MinorUnits); Assert.Equal("ARS", variant.SalePrice.Currency);
        Assert.Single((await scenario.Catalog.VariantsAsync(product)).Items);
        Assert.Single((await scenario.Catalog.SearchAsync(input.Code.ToLowerInvariant())).Items);
        await using var db = await scenario.Factory.CreateDbContextAsync();
        Assert.Single(await db.AuditEntries.Where(x => x.Action == "variant.created").ToListAsync());
        if (category is ProductCategory.Lens or ProductCategory.ContactLens)
        {
            var values = await db.Database.SqlQueryRaw<int>("SELECT SphereMilli AS Value FROM ProductVariants").ToListAsync();
            Assert.Equal(1125, Assert.Single(values));
            Assert.Null(variant.Technical.Specification.Lens?.Addition ?? variant.Technical.Specification.Contact?.Addition);
        }
    }

    [Fact]
    public async Task ProfessionalReceivesOnlyTechnicalContractsAndCannotAccessPricesOrManageCatalog()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync(); using var admin = await CatalogScenario.OpenAsync(app);
        var product = await ProductAsync(admin); var input = await InputAsync(admin, product);
        input.Frame = new("Color ficticio", 52, 18, 140);
        var variant = await admin.Catalog.CreateVariantAsync(product, input); Assert.True(variant.Succeeded);
        using var professional = await CatalogScenario.OpenAsync(app, "profesional.ficticio");
        var technical = await professional.Technical.VariantAsync(product, variant.Id!.Value);
        Assert.NotNull(technical); Assert.DoesNotContain("price", JsonSerializer.Serialize(technical), StringComparison.OrdinalIgnoreCase);
        Assert.Single((await professional.Technical.SearchAsync(null)).Items);
        Assert.Single((await professional.Technical.VariantsAsync(product)).Items);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => professional.Catalog.SearchAsync(null));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => professional.Catalog.GetAsync(product));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => professional.Catalog.VariantAsync(product, variant.Id.Value));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => professional.Catalog.VariantsAsync(product));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => professional.Catalog.ConfigurationAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => professional.Catalog.CreateAsync(new()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => professional.Catalog.UpdateVariantAsync(product, variant.Id.Value, input));
        using var sales = await CatalogScenario.OpenAsync(app, "ventas.ficticio");
        Assert.Equal(1235, (await sales.Catalog.VariantAsync(product, variant.Id.Value))!.SalePrice!.MinorUnits);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sales.Catalog.CreateVariantAsync(product, input));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sales.Catalog.ConfigureCurrencyAsync("USD", Guid.Empty));
    }

    [Fact]
    public async Task DuplicateCodesStaleVersionsAndCrossProductChangesAreRejectedAtomically()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync(); using var scenario = await CatalogScenario.OpenAsync(app);
        var a = await ProductAsync(scenario); var b = await ProductAsync(scenario, name: "Otro producto ficticio");
        var input = await InputAsync(scenario, a); var saved = await scenario.Catalog.CreateVariantAsync(a, input);
        Assert.True(saved.Succeeded);
        var duplicate = await InputAsync(scenario, b, " sku-fic ");
        Assert.Equal(CatalogResultCode.Duplicate, (await scenario.Catalog.CreateVariantAsync(b, duplicate)).Code);
        var original = (await scenario.Catalog.VariantAsync(a, saved.Id!.Value))!;
        input.ProductVersion = (await scenario.Catalog.GetAsync(a))!.Product.Version; input.Version = original.Technical.Version; input.SalePrice = 20;
        Assert.True((await scenario.Catalog.UpdateVariantAsync(a, saved.Id.Value, input)).Succeeded);
        Assert.Equal(1235, original.SalePrice!.MinorUnits);
        Assert.Equal(CatalogResultCode.Conflict, (await scenario.Catalog.UpdateVariantAsync(a, saved.Id.Value, input)).Code);
        Assert.Equal(CatalogResultCode.Conflict, (await scenario.Catalog.UpdateVariantAsync(b, saved.Id.Value, duplicate)).Code);
        Assert.Null(await scenario.Catalog.VariantAsync(b, saved.Id.Value));
        await using var db = await scenario.Factory.CreateDbContextAsync();
        Assert.Single(await db.ProductVariants.ToListAsync());
        Assert.Single(await db.AuditEntries.Where(x => x.Action == "variant.updated").ToListAsync());
    }

    [Fact]
    public async Task CurrencyIsIndependentPerInstallationAndCannotRelabelExistingPrices()
    {
        await using var a = new SecurityApp(); await a.InitializeAsync(); using var first = await CatalogScenario.OpenAsync(a);
        await using var b = new SecurityApp(); await b.InitializeAsync(); using var second = await CatalogScenario.OpenAsync(b);
        var original = await first.Catalog.ConfigurationAsync();
        Assert.True((await first.Catalog.ConfigureCurrencyAsync("USD", original.Version)).Succeeded);
        Assert.Equal("USD", (await first.Catalog.ConfigurationAsync()).Currency);
        Assert.Equal("ARS", (await second.Catalog.ConfigurationAsync()).Currency);
        Assert.Equal(CatalogResultCode.Conflict, (await first.Catalog.ConfigureCurrencyAsync("ARS", original.Version)).Code);
        var product = await ProductAsync(second); var input = await InputAsync(second, product);
        Assert.True((await second.Catalog.CreateVariantAsync(product, input)).Succeeded);
        var configuration = await second.Catalog.ConfigurationAsync();
        Assert.Equal(CatalogResultCode.Invalid, (await second.Catalog.ConfigureCurrencyAsync("USD", configuration.Version)).Code);
        Assert.Equal("ARS", (await second.Catalog.ConfigurationAsync()).Currency);
    }
}
