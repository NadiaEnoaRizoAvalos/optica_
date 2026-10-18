using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Commerce;
using OpticaSosa.Application.Inventory;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Commerce;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Domain.Technical;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class OrderTechnicalBoundaryTests
{
    private static async Task<(Guid Publication, Guid Version)> PublishBothAsync(InventoryScenario test, bool samePrescription)
    {
        if (samePrescription)
        {
            var before = (await test.Test.Clinical.Clinical.GetAsync(test.Test.CustomerId, test.Test.RecordId))!;
            test.Test.Source.Left = test.Test.Source.Right;
            test.Test.Source.Version = before.Version; test.Test.Source.SubmissionId = Guid.NewGuid(); test.Test.Source.CorrectionReason = "Ambos ojos ficticios coincidentes";
            Assert.True((await test.Test.Clinical.Clinical.CorrectAsync(test.Test.CustomerId, test.Test.RecordId, test.Test.Source)).Succeeded);
        }
        var input = await test.Test.InputAsync(); input.IncludeLeft = true;
        if ((await test.Catalog.Catalog.GetAsync(test.ProductId))!.Product.Category == ProductCategory.ContactLens)
        {
            input.Purpose = TechnicalPurpose.ContactLenses; input.Mounting = null;
            input.RightContact = new(test.VariantId, 8.6m, 14.2m); input.LeftContact = input.RightContact;
        }
        var result = await test.Test.Publications.PublishAsync(test.Test.CustomerId, test.Test.RecordId, input);
        Assert.True(result.Succeeded, result.Error);
        var pub = (await test.Test.Publications.GetAsync(test.Test.CustomerId, result.Id!.Value))!;
        return (pub.Id, pub.Version);
    }
    [Fact]
    public async Task AClosedCompatibleContactBoxCanCoverBothEyesWithoutSplittingTheBox()
    {
        await using var test = await InventoryScenario.CreateAsync(ProductCategory.ContactLens);
        var batch = await test.ReceiveAsync(lot: "LOTE-FIC", expiry: new(2027, 1, 1), precision: ExpiryPrecision.Day);
        var publication = await PublishBothAsync(test, true);
        var assignment = Guid.NewGuid();
        Assert.True((await test.Technical.AssignAsync(test.OrderId, test.LineId, new(assignment,
            (await test.Sales.Orders.GetAsync(test.OrderId))!.Version, publication.Publication, publication.Version, TechnicalEyes.Both))).Succeeded);
        Assert.True((await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batch, assignment: assignment))).Succeeded);
        var reservation = Assert.Single((await test.Stock.ReservationsAsync(test.OrderId)).Items); Assert.Equal(1, reservation.Quantity);
        var projection = (await test.Technical.ProjectionAsync(test.OrderId, test.LineId))!;
        Assert.Equal(TechnicalEyes.Both, projection.Eyes); Assert.Equal(projection.Right, projection.Left);
        Assert.Equal(6, Assert.Single((await test.Sales.Orders.LinesAsync(test.OrderId))!.Items).UnitsPerPackage);
    }
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public async Task BothEyesNeedTwoIndividualCrystalsAndStockMatchingEachEye(int quantity, bool expected)
    {
        await using var test = await InventoryScenario.CreateAsync(ProductCategory.Lens, quantity);
        var publication = await PublishBothAsync(test, true);
        var result = await test.Technical.AssignAsync(test.OrderId, test.LineId, new(Guid.NewGuid(),
            (await test.Sales.Orders.GetAsync(test.OrderId))!.Version, publication.Publication, publication.Version, TechnicalEyes.Both));
        Assert.Equal(expected, result.Succeeded);
    }
    [Fact]
    public async Task StockCannotSubstituteASelectedVariantForDifferentPowers()
    {
        await using var test = await InventoryScenario.CreateAsync(ProductCategory.Lens, quantity: 2);
        var publication = await PublishBothAsync(test, false);
        Assert.Equal(SalesResultCode.Invalid, (await test.Technical.AssignAsync(test.OrderId, test.LineId, new(Guid.NewGuid(),
            (await test.Sales.Orders.GetAsync(test.OrderId))!.Version, publication.Publication, publication.Version, TechnicalEyes.Both))).Code);
        await using var db = await test.Sales.Factory.CreateDbContextAsync(); Assert.Empty(await db.OrderTechnicalAssignments.ToListAsync());
    }
    [Fact]
    public async Task ActiveReservationsMustBeReleasedBeforeChangingTechnicalAssignment()
    {
        await using var test = await InventoryScenario.CreateAsync(ProductCategory.Lens);
        var batch = await test.ReceiveAsync(); var oldAssignment = await test.AssignAsync();
        Assert.True((await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batch, assignment: oldAssignment))).Succeeded);
        var published = await test.Test.Publications.PublishAsync(test.Test.CustomerId, test.Test.RecordId, await test.Test.InputAsync());
        var pub = (await test.Test.Publications.GetAsync(test.Test.CustomerId, published.Id!.Value))!;
        Assert.Equal(SalesResultCode.Invalid, (await test.Technical.AssignAsync(test.OrderId, test.LineId, new(Guid.NewGuid(),
            (await test.Sales.Orders.GetAsync(test.OrderId))!.Version, pub.Id, pub.Version, TechnicalEyes.Right, oldAssignment, "Ficticio"))).Code);
        Assert.Equal(oldAssignment, (await test.Technical.ProjectionAsync(test.OrderId, test.LineId))!.AssignmentId);
    }
    [Fact]
    public async Task UnreceivedLaboratoryMaterialCannotBeReservedFromGenericStock()
    {
        await using var test = await InventoryScenario.CreateAsync(ProductCategory.Lens, source: FulfilmentSource.Laboratory);
        var batch = await test.ReceiveAsync(); var assignment = await test.AssignAsync();
        Assert.Equal(SalesResultCode.Invalid, (await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batch, assignment: assignment))).Code);
        Assert.Equal(0, Assert.Single((await test.Stock.AvailabilityAsync(test.OrderId)).Items).Available);
        Assert.Equal(1, Assert.Single((await test.Stock.BatchesAsync()).Items).Available);
    }
}
