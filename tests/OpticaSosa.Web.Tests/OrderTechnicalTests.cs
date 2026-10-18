using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Commerce;
using OpticaSosa.Application.Inventory;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Domain.Technical;
using OpticaSosa.Infrastructure.Inventory;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class OrderTechnicalTests
{
    [Theory]
    [InlineData(ProductCategory.Lens)]
    [InlineData(ProductCategory.ContactLens)]
    public async Task StockCannotBeReservedUntilTheProfessionalExplicitlyAssignsValidTechnicalData(ProductCategory category)
    {
        await using var test = await InventoryScenario.CreateAsync(category);
        var batch = category == ProductCategory.ContactLens
            ? await test.ReceiveAsync(lot: "LOTE-FIC", expiry: new(2027, 10, 17), precision: ExpiryPrecision.Day) : await test.ReceiveAsync();
        Assert.Equal(SalesResultCode.Invalid, (await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batch))).Code);
        var assignment = await test.AssignAsync();
        var availability = Assert.Single((await test.Stock.AvailabilityAsync(test.OrderId)).Items);
        Assert.True(availability.TechnicalRequired); Assert.True(availability.TechnicalCurrent);
        Assert.True((await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batch, assignment: assignment))).Succeeded);
        var projection = (await new OrderTechnicalService(test.Sales.Factory, test.Sales.Actor, test.Clock).ProjectionAsync(test.OrderId, test.LineId))!;
        Assert.Equal(1.125m, projection.Right.Sphere); Assert.Null(projection.Left.Sphere);
        var json = JsonSerializer.Serialize(projection);
        foreach (var forbidden in new[] { "NOTA_CLINICA_FICTICIA_NO_PUBLICABLE", "SINTOMA_FICTICIO_NO_PUBLICABLE", "Price", "CorrectionReason", "RevocationReason" })
            Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
        Assert.Null(await test.Technical.ProjectionAsync(Guid.NewGuid(), test.LineId));
    }
    [Fact]
    public async Task OnlyProfessionalAssignsAndNoFinancialDataIsReturnedToTheProfessionalQueue()
    {
        await using var test = await InventoryScenario.CreateAsync(ProductCategory.Lens);
        var rows = await test.Technical.LinesForProfessionalAsync(test.Test.CustomerId);
        Assert.Equal(test.LineId, Assert.Single(rows.Items).LineId);
        var json = JsonSerializer.Serialize(rows); Assert.DoesNotContain("price", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("paid", json, StringComparison.OrdinalIgnoreCase);
        var salesTechnical = new OrderTechnicalService(test.Sales.Factory, test.Sales.Actor, test.Clock);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => salesTechnical.LinesForProfessionalAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => salesTechnical.AssignAsync(test.OrderId, test.LineId,
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TechnicalEyes.Right)));
    }
    [Fact]
    public async Task ClinicalCorrectionBlocksNewReservationsAndKeepsTheOldProjectionAndReservedStock()
    {
        await using var test = await InventoryScenario.CreateAsync(ProductCategory.Lens, quantity: 2);
        var batch = await test.ReceiveAsync(2); var assignment = await test.AssignAsync();
        Assert.True((await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batch, assignment: assignment))).Succeeded);
        var before = (await test.Test.Clinical.Clinical.GetAsync(test.Test.CustomerId, test.Test.RecordId))!;
        test.Test.Source.Version = before.Version; test.Test.Source.SubmissionId = Guid.NewGuid();
        test.Test.Source.CorrectionReason = "Corrección ficticia"; test.Test.Source.Right.Sphere = 1.375m;
        Assert.True((await test.Test.Clinical.Clinical.CorrectAsync(test.Test.CustomerId, test.Test.RecordId, test.Test.Source)).Succeeded);
        Assert.Equal(SalesResultCode.Invalid, (await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batch, assignment: assignment))).Code);
        var projection = (await test.Technical.ProjectionAsync(test.OrderId, test.LineId))!;
        Assert.Equal(PublicationStatus.SourceChanged, projection.Status); Assert.Equal(1.125m, projection.Right.Sphere);
        Assert.Equal(1, Assert.Single((await test.Stock.BatchesAsync()).Items).Reserved);
        Assert.False(Assert.Single((await test.Stock.AvailabilityAsync(test.OrderId)).Items).TechnicalCurrent);
    }
    [Fact]
    public async Task RevokedPublicationBlocksReservationAndReplacementPreservesAssignmentHistory()
    {
        await using var test = await InventoryScenario.CreateAsync(ProductCategory.Lens); var batch = await test.ReceiveAsync();
        var assignment = await test.AssignAsync(); var projection = (await test.Technical.ProjectionAsync(test.OrderId, test.LineId))!;
        var old = (await test.Test.Publications.GetAsync(test.Test.CustomerId, projection.PublicationId))!;
        Assert.True((await test.Test.Publications.RevokeAsync(test.Test.CustomerId, old.Id, old.Version, "Revocación ficticia")).Succeeded);
        Assert.Equal(SalesResultCode.Invalid, (await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batch, assignment: assignment))).Code);
        var published = await test.Test.Publications.PublishAsync(test.Test.CustomerId, test.Test.RecordId, await test.Test.InputAsync());
        Assert.True(published.Succeeded);
        var current = (await test.Test.Publications.GetAsync(test.Test.CustomerId, published.Id!.Value))!;
        var input = new AssignTechnicalInput(Guid.NewGuid(), (await test.Sales.Orders.GetAsync(test.OrderId))!.Version, current.Id, current.Version,
            TechnicalEyes.Right, assignment, "Vincular la autorización actualizada ficticia");
        Assert.True((await test.Technical.AssignAsync(test.OrderId, test.LineId, input)).Succeeded);
        Assert.True((await test.Technical.AssignAsync(test.OrderId, test.LineId, input)).Succeeded);
        await using var db = await test.Sales.Factory.CreateDbContextAsync();
        var history = await db.OrderTechnicalAssignments.ToListAsync(); Assert.Equal(2, history.Count);
        Assert.False(history.Single(x => x.Id == assignment).IsCurrent); Assert.Equal(assignment, history.Single(x => x.IsCurrent).PreviousId);
        Assert.True((await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batch, assignment: input.SubmissionId))).Succeeded);
    }
}
