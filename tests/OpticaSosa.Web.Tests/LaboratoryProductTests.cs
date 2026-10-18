using OpticaSosa.Application.Commerce;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Commerce;
using OpticaSosa.Domain.Inventory;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class LaboratoryProductTests
{
    [Fact]
    public async Task LaboratoryCannotSilentlyOverridePowersExplicitlyDeclaredInTheSku()
    {
        await using var test = await LaboratoryScenario.CreateAsync(assign: false); var inventory = test.Inventory;
        var input = await CatalogTests.InputAsync(inventory.Catalog, inventory.ProductId, "LAB-FIJO-FICTICIO");
        input.Lens = new("Material ficticio", 1.6m, "Tratamiento ficticio", 1.375m, 0m, 180);
        var variant = await inventory.Catalog.Catalog.CreateVariantAsync(inventory.ProductId, input); Assert.True(variant.Succeeded);
        var draft = await inventory.Sales.DraftAsync(inventory.Catalog, inventory.Test.CustomerId, variant.Id!.Value);
        draft.Lines = [draft.Lines[0] with { Source = FulfilmentSource.Laboratory }];
        var orderId = await inventory.Sales.ConfirmAsync(draft);
        var line = Assert.Single((await inventory.Sales.Orders.LinesAsync(orderId))!.Items);
        var publication = await inventory.Test.Publications.PublishAsync(inventory.Test.CustomerId, inventory.Test.RecordId, await inventory.Test.InputAsync());
        Assert.True(publication.Succeeded);
        var pub = (await inventory.Test.Publications.GetAsync(inventory.Test.CustomerId, publication.Id!.Value))!;
        var result = await inventory.Technical.AssignAsync(orderId, line.Id, new(Guid.NewGuid(), (await inventory.Sales.Orders.GetAsync(orderId))!.Version,
            pub.Id, pub.Version, TechnicalEyes.Right));
        Assert.Equal(SalesResultCode.Invalid, result.Code); Assert.Contains("graduación explícita", result.Error);
    }
    [Fact]
    public async Task ClosedContactSkuCannotSilentlyDiscardAnUnsupportedPrism()
    {
        await using var test = await LaboratoryScenario.CreateAsync(category: ProductCategory.ContactLens, assign: false); var inventory = test.Inventory;
        var record = (await inventory.Test.Clinical.Clinical.GetAsync(inventory.Test.CustomerId, inventory.Test.RecordId))!;
        inventory.Test.Source.SubmissionId = Guid.NewGuid(); inventory.Test.Source.Version = record.Version;
        inventory.Test.Source.Right.Prism = .125m; inventory.Test.Source.Right.Base = "Base ficticia"; inventory.Test.Source.CorrectionReason = "Prisma ficticio explícito";
        Assert.True((await inventory.Test.Clinical.Clinical.CorrectAsync(inventory.Test.CustomerId, inventory.Test.RecordId, inventory.Test.Source)).Succeeded);
        var input = await inventory.Test.InputAsync(); input.Purpose = OpticaSosa.Domain.Technical.TechnicalPurpose.ContactLenses; input.Mounting = null;
        input.RightContact = new(inventory.VariantId, 8.6m, 14.2m);
        Assert.False((await inventory.Test.Publications.PublishAsync(inventory.Test.CustomerId, inventory.Test.RecordId, input)).Succeeded);
        Assert.Equal(.125m, (await inventory.Test.Clinical.Clinical.GetAsync(inventory.Test.CustomerId, inventory.Test.RecordId))!.Data.Right.Prism);
    }
}
