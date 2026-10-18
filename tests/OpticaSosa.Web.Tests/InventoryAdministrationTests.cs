using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Inventory;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Infrastructure.Inventory;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class InventoryAdministrationTests
{
    [Fact]
    public async Task AdjustmentsAreAdminOnlyIdempotentAndCannotRemoveReservedMaterial()
    {
        await using var test = await InventoryScenario.CreateAsync();
        using var admin = await CommerceScenario.OpenAsync(test.Test.App);
        var service = new InventoryAdministration(admin.Factory, admin.Actor, test.Clock);
        var sales = new InventoryAdministration(test.Sales.Factory, test.Sales.Actor, test.Clock);
        var batch = await test.ReceiveAsync(2);
        var input = new InventoryAdjustmentInput(Guid.NewGuid(), (await test.Stock.BatchAsync(batch))!.Version, 1, false, "Recuento ficticio");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sales.AdjustAsync(batch, input));
        Assert.True((await service.AdjustAsync(batch, input)).Succeeded);
        Assert.True((await service.AdjustAsync(batch, input)).Succeeded);
        Assert.True((await service.AdjustAsync(batch, input with { Delta = 2 })).Conflict);
        Assert.True((await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batch))).Succeeded);
        var version = (await test.Stock.BatchAsync(batch))!.Version;
        Assert.False((await service.AdjustAsync(batch, new(Guid.NewGuid(), version, -3, false, "No retirar reservas"))).Succeeded);
        Assert.True((await service.AdjustAsync(batch, new(Guid.NewGuid(), version, -2, false, "Recuento restante"))).Succeeded);
        var result = (await test.Stock.BatchAsync(batch))!;
        Assert.Equal(1, result.OnHand); Assert.Equal(1, result.Reserved);
        Assert.True((await service.ReconciliationAsync(batch))!.Consistent);
        await using var db = await admin.Factory.CreateDbContextAsync();
        Assert.Equal(2, await db.AuditEntries.CountAsync(x => x.Action == "inventory.adjusted"));
    }

    [Fact]
    public async Task QuarantineRequiresReviewAndAdminAloneCannotReleaseIt()
    {
        await using var test = await InventoryScenario.CreateAsync();
        using var admin = await CommerceScenario.OpenAsync(test.Test.App);
        var administrator = new InventoryAdministration(admin.Factory, admin.Actor, test.Clock);
        var sales = new InventoryAdministration(test.Sales.Factory, test.Sales.Actor, test.Clock);
        var batch = await test.ReceiveAsync(3, quarantine: 2);
        var hold = Assert.Single((await sales.HoldsAsync(batch)).Items);
        var review = new InventoryReviewInput(Guid.NewGuid(), (await test.Stock.BatchAsync(batch))!.Version,
            hold.Version, 1, true, true, true, null, "Revisión ficticia conforme");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => administrator.ReviewAsync(batch, hold.Id, review));
        Assert.True((await sales.ReviewAsync(batch, hold.Id, review)).Succeeded);
        Assert.True((await sales.ReviewAsync(batch, hold.Id, review)).Succeeded);
        Assert.Equal(2, (await test.Stock.BatchAsync(batch))!.Available);
        hold = Assert.Single((await sales.HoldsAsync(batch)).Items);
        var reject = review with { SubmissionId = Guid.NewGuid(), BatchVersion = (await test.Stock.BatchAsync(batch))!.Version,
            HoldVersion = hold.Version, Approve = false, ItemIntact = false, Reason = "Permanece observado" };
        Assert.True((await sales.ReviewAsync(batch, hold.Id, reject)).Succeeded);
        Assert.Equal(1, Assert.Single((await sales.HoldsAsync(batch)).Items).Pending);
        Assert.Equal(2, (await sales.ReviewsAsync(hold.Id)).Items.Count);
        Assert.True((await sales.ReconciliationAsync(batch))!.Consistent);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public async Task OnlyProfessionalCanReleaseIntactUnexpiredContactQuarantine(bool opened, bool expired, bool expected)
    {
        await using var test = await InventoryScenario.CreateAsync(ProductCategory.ContactLens);
        var receipt = await test.ReceiptAsync(1, 1, "CONTACTO-FICTICIO", new(2026, 10, 17), ExpiryPrecision.Day);
        var received = await test.Stock.ReceiveAsync(receipt with { QuarantinedContactPackageOpened = opened });
        Assert.True(received.Succeeded, received.Error); var batch = received.BatchId!.Value;
        if (expired) test.Clock.Now = test.Clock.Now.AddDays(1);
        var professional = new InventoryAdministration(test.Sales.Factory, test.Test.Clinical.Actor, test.Clock);
        var sales = new InventoryAdministration(test.Sales.Factory, test.Sales.Actor, test.Clock);
        var hold = Assert.Single((await professional.HoldsAsync(batch)).Items);
        var input = new InventoryReviewInput(Guid.NewGuid(), (await test.Stock.BatchAsync(batch))!.Version,
            hold.Version, 1, true, true, true, true, "Revisión ficticia de contactos");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sales.ReviewAsync(batch, hold.Id, input));
        Assert.Equal(expected, (await professional.ReviewAsync(batch, hold.Id, input)).Succeeded);
        Assert.Equal(expected ? 0 : 1, (await test.Stock.BatchAsync(batch))!.Quarantined);
        Assert.True((await professional.ReconciliationAsync(batch))!.Consistent);
    }

    [Fact]
    public async Task WithdrawalRecordsPhysicalLossWithoutChangingMoneyOrReservations()
    {
        await using var test = await InventoryScenario.CreateAsync();
        using var admin = await CommerceScenario.OpenAsync(test.Test.App);
        var service = new InventoryAdministration(admin.Factory, admin.Actor, test.Clock);
        var batch = await test.ReceiveAsync(3, quarantine: 1);
        var hold = Assert.Single((await service.HoldsAsync(batch)).Items);
        var input = new InventoryWithdrawalInput(Guid.NewGuid(), (await test.Stock.BatchAsync(batch))!.Version,
            1, "Baja ficticia por rotura", hold.Id, hold.Version);
        Assert.True((await service.WriteOffAsync(batch, input)).Succeeded);
        Assert.True((await service.WriteOffAsync(batch, input)).Succeeded);
        Assert.Empty((await service.HoldsAsync(batch)).Items);
        var closed = Assert.Single((await service.HoldsAsync(batch, true)).Items);
        Assert.Equal(1, closed.Removed);
        Assert.Equal(2, (await test.Stock.BatchAsync(batch))!.Available);
        Assert.True((await service.ReconciliationAsync(batch))!.Consistent);
        await using var db = await admin.Factory.CreateDbContextAsync();
        Assert.Single(await db.StockWithdrawals.ToListAsync()); Assert.Empty(await db.OrderMoneyEntries.ToListAsync());
    }

    [Fact]
    public async Task StaleVersionsAndAuditFailureLeaveCountsAndQuarantineHistoryIntact()
    {
        await using var test = await InventoryScenario.CreateAsync();
        var service = new InventoryAdministration(test.Sales.Factory, test.Sales.Actor, test.Clock);
        var batch = await test.ReceiveAsync(2);
        var version = (await test.Stock.BatchAsync(batch))!.Version;
        Assert.True((await service.QuarantineAsync(batch, new(Guid.NewGuid(), version, 1, "Observación ficticia"))).Succeeded);
        Assert.True((await service.QuarantineAsync(batch, new(Guid.NewGuid(), version, 1, "Versión anterior"))).Conflict);
        await using var db = await test.Sales.Factory.CreateDbContextAsync();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectFictitiousInventoryAudit BEFORE INSERT ON AuditEntries WHEN NEW.Action = 'inventory.quarantined' BEGIN SELECT RAISE(ABORT, 'Fictitious audit failure'); END;");
        Assert.False((await service.QuarantineAsync(batch, new(Guid.NewGuid(), (await test.Stock.BatchAsync(batch))!.Version, 1, "Debe revertirse"))).Succeeded);
        Assert.Single((await service.HoldsAsync(batch)).Items);
        Assert.Equal(1, (await test.Stock.BatchAsync(batch))!.Quarantined);
        Assert.True((await service.ReconciliationAsync(batch))!.Consistent);
    }

    [Fact]
    public async Task ConcurrentWriteOffsCannotWithdrawTheSameAvailableUnitTwice()
    {
        await using var test = await InventoryScenario.CreateAsync();
        using var admin = await CommerceScenario.OpenAsync(test.Test.App);
        var service = new InventoryAdministration(admin.Factory, admin.Actor, test.Clock);
        var batch = await test.ReceiveAsync(); var version = (await test.Stock.BatchAsync(batch))!.Version;
        var results = await Task.WhenAll(service.WriteOffAsync(batch, new(Guid.NewGuid(), version, 1, "Rotura ficticia A")),
            service.WriteOffAsync(batch, new(Guid.NewGuid(), version, 1, "Rotura ficticia B")));
        Assert.Single(results, x => x.Succeeded); Assert.Single(results, x => x.Conflict);
        Assert.Equal(0, (await test.Stock.BatchAsync(batch))!.OnHand);
        Assert.True((await service.ReconciliationAsync(batch))!.Consistent);
    }

    [Fact]
    public async Task SupplierReturnRetainsItsReferenceAndDoesNotGenerateAMonetaryRefund()
    {
        await using var test = await InventoryScenario.CreateAsync();
        using var admin = await CommerceScenario.OpenAsync(test.Test.App);
        var service = new InventoryAdministration(admin.Factory, admin.Actor, test.Clock);
        var supplier = await test.Catalog.Suppliers.CreateAsync(new() { Name = "Proveedor ficticio para devolución" });
        Assert.True(supplier.Succeeded);
        var batch = await test.ReceiveAsync(); var version = (await test.Stock.BatchAsync(batch))!.Version;
        Assert.False((await service.ReturnToSupplierAsync(batch, new(Guid.NewGuid(), version, 1, "Falta proveedor"))).Succeeded);
        Assert.True((await service.ReturnToSupplierAsync(batch, new(Guid.NewGuid(), version, 1, "Devolución física ficticia", SupplierId: supplier.Id))).Succeeded);
        await using var db = await admin.Factory.CreateDbContextAsync();
        Assert.Equal(supplier.Id, (await db.StockWithdrawals.SingleAsync()).SupplierId);
        Assert.Empty(await db.OrderMoneyEntries.ToListAsync());
        Assert.True((await service.ReconciliationAsync(batch))!.Consistent);
    }

    [Fact]
    public async Task CustomizedLensQuarantineCannotBeReleasedAfterItsAuthorizationIsRevoked()
    {
        await using var lab = await LaboratoryScenario.CreateAsync(quantity: 1);
        var id = await lab.AuthorizeAsync(1); await lab.DispatchAsync(id);
        Assert.True((await lab.Laboratory.ReceiveAsync(lab.Inventory.OrderId, id, await lab.ReceiptAsync(id, 1, 1))).Succeeded);
        await using var db = await lab.Inventory.Sales.Factory.CreateDbContextAsync();
        var assignment = await db.OrderTechnicalAssignments.SingleAsync(x => x.Id == lab.AssignmentId);
        var publication = (await lab.Inventory.Test.Publications.GetAsync(lab.Inventory.Test.CustomerId, assignment.PublicationId))!;
        Assert.True((await lab.Inventory.Test.Publications.RevokeAsync(lab.Inventory.Test.CustomerId, publication.Id,
            publication.Version, "Revocación ficticia")).Succeeded);
        var service = new InventoryAdministration(lab.Inventory.Sales.Factory, lab.Inventory.Test.Clinical.Actor, lab.Inventory.Clock);
        var hold = Assert.Single((await service.HoldsAsync()).Items);
        var batch = (await lab.Inventory.Stock.BatchAsync(hold.BatchId))!;
        Assert.False((await service.ReviewAsync(batch.Id, hold.Id, new(Guid.NewGuid(), batch.Version, hold.Version,
            1, true, true, true, null, "No reutilizar autorización revocada"))).Succeeded);
        Assert.Equal(1, (await lab.Inventory.Stock.BatchAsync(batch.Id))!.Quarantined);
        Assert.True((await service.ReconciliationAsync(batch.Id))!.Consistent);
    }

    [Fact]
    public async Task HoldsAndReviewsArePaginatedAndAlertsUseTheRequestedHorizon()
    {
        await using var test = await InventoryScenario.CreateAsync();
        var service = new InventoryAdministration(test.Sales.Factory, test.Sales.Actor, test.Clock);
        var batch = await test.ReceiveAsync(52);
        for (var i = 0; i < 52; i++)
            Assert.True((await service.QuarantineAsync(batch, new(Guid.NewGuid(), (await test.Stock.BatchAsync(batch))!.Version, 1, "Observación ficticia"))).Succeeded);
        var first = await service.HoldsAsync(batch); var second = await service.HoldsAsync(batch, page: 2);
        Assert.Equal(50, first.Items.Count); Assert.True(first.HasMore); Assert.Equal(2, second.Items.Count);
        Assert.Empty(first.Items.Select(x => x.Id).Intersect(second.Items.Select(x => x.Id)));
        Assert.Single((await test.Stock.AlertsAsync(new(2026, 10, 17))).Items);
        Assert.True((await service.ReconciliationAsync(batch))!.Consistent);
    }
}
