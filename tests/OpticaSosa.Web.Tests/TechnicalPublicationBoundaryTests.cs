using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Catalog;
using OpticaSosa.Application.Technical;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Technical;
using OpticaSosa.Infrastructure.Technical;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class TechnicalPublicationBoundaryTests
{
    private static async Task<(Guid Product, Guid Variant)> ContactAsync(CatalogScenario catalog, decimal sphere)
    {
        var product = await CatalogTests.ProductAsync(catalog, ProductCategory.ContactLens);
        var input = await CatalogTests.InputAsync(catalog, product, "CONTACTO-FIC");
        input.UnitsPerPackage = 6; input.Contact = new(sphere, 0m, null, null, 8.60m, 14.20m);
        var variant = await catalog.Catalog.CreateVariantAsync(product, input);
        Assert.True(variant.Succeeded); return (product, variant.Id!.Value);
    }

    [Theory]
    [InlineData("1.125", true)]
    [InlineData("1.375", false)]
    public async Task ContactsRequireExactAuthorizedProductWithoutConvertingThePrescription(string sphere, bool expected)
    {
        await using var test = await TechnicalTestContext.CreateAsync(); using var catalog = await CatalogScenario.OpenAsync(test.App);
        var selected = await ContactAsync(catalog, decimal.Parse(sphere, System.Globalization.CultureInfo.InvariantCulture));
        var input = await test.InputAsync(); input.Purpose = TechnicalPurpose.ContactLenses;
        input.ProductId = selected.Product; input.Mounting = null;
        input.RightContact = new(selected.Variant, 8.60m, 14.20m);
        var result = await test.Publications.PublishAsync(test.CustomerId, test.RecordId, input);
        Assert.Equal(expected, result.Succeeded);
        var variant = (await catalog.Catalog.VariantAsync(selected.Product, selected.Variant))!;
        Assert.Equal(expected, variant.Technical.SpecificationLocked);
        await using var db = await test.Clinical.Factory.CreateDbContextAsync();
        Assert.Equal(expected ? 1 : 0, await db.TechnicalPublications.CountAsync());
        if (expected)
        {
            var publication = (await test.Publications.GetAsync(test.CustomerId, result.Id!.Value))!;
            Assert.Null(publication.PupillaryDistance);
            Assert.Equal(8.60m, publication.Request.RightContact!.BaseCurveMm);
            var edit = await CatalogTests.InputAsync(catalog, selected.Product, "OTRO-CODIGO-FIC");
            edit.Version = variant.Technical.Version; edit.UnitsPerPackage = 6; edit.Contact = variant.Technical.Specification.Contact;
            Assert.Equal(CatalogResultCode.Invalid, (await catalog.Catalog.UpdateVariantAsync(selected.Product, selected.Variant, edit)).Code);
        }
        Assert.Equal(1.125m, (await test.Clinical.Clinical.GetAsync(test.CustomerId, test.RecordId))!.Data.Right.Sphere);
    }

    [Fact]
    public async Task ConcurrentPublicationOfSameSourceVersionHasOneAtomicWinner()
    {
        await using var test = await TechnicalTestContext.CreateAsync();
        var first = await test.InputAsync(); var second = await test.InputAsync();
        var another = new TechnicalPublicationService(test.Clinical.Factory, test.Clinical.Actor, TimeProvider.System);
        var results = await Task.WhenAll(test.Publications.PublishAsync(test.CustomerId, test.RecordId, first),
            another.PublishAsync(test.CustomerId, test.RecordId, second));
        Assert.Single(results, x => x.Succeeded);
        await using var db = await test.Clinical.Factory.CreateDbContextAsync();
        Assert.Single(await db.TechnicalPublications.ToListAsync());
        Assert.Single(await db.AuditEntries.Where(x => x.Action == "technical.publication.created").ToListAsync());
    }

    [Fact]
    public async Task SeveralIndependentPublicationsOfSameRecipeRemainPaginatedAndPreserveTheirOwnMounting()
    {
        await using var test = await TechnicalTestContext.CreateAsync();
        for (var i = 0; i < 52; i++)
        {
            var input = await test.InputAsync(); input.Mounting = new(rightHeightMm: 15m + i / 100m);
            Assert.True((await test.Publications.PublishAsync(test.CustomerId, test.RecordId, input)).Succeeded);
        }
        var first = await test.Publications.ListAsync(test.CustomerId, test.RecordId);
        var second = await test.Publications.ListAsync(test.CustomerId, test.RecordId, page: 2);
        Assert.Equal(50, first.Items.Count); Assert.True(first.HasMore); Assert.Equal(2, second.Items.Count); Assert.False(second.HasMore);
        Assert.Empty(first.Items.Select(x => x.Id).Intersect(second.Items.Select(x => x.Id)));
        Assert.All(first.Items.Concat(second.Items), x => Assert.Equal(PublicationStatus.Current, x.Status));
        Assert.Equal(52, first.Items.Concat(second.Items).Select(x => x.Request.Mounting.RightHeightMm).Distinct().Count());
    }
}
