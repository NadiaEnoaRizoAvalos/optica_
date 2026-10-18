using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Infrastructure.Inventory;
using OpticaSosa.Infrastructure.Persistence;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class StockRecoveryTests
{
    [Fact]
    public async Task ExistingBackupRecoversStockLedgerReservationsAndTechnicalAuthorizationTogether()
    {
        await using var test = await InventoryScenario.CreateAsync(ProductCategory.Lens, quantity: 2);
        var batchId = await test.ReceiveAsync(3, quarantine: 1); var assignment = await test.AssignAsync();
        Assert.True((await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batchId, 2, assignment))).Succeeded);
        using var snapshotActor = await ClinicalScenario.OpenAsync(test.Test.App, "admin.ficticio");
        var snapshot = await snapshotActor.Snapshots.CreateAsync(); Assert.Equal("Complete", snapshot.Status);
        var file = Path.Combine(snapshot.Directory, "optica.db"); var hash = SHA256.HashData(await File.ReadAllBytesAsync(file));
        await using var restored = new SecurityApp();
        using (var scope = restored.Services.CreateScope())
        {
            var paths = scope.ServiceProvider.GetRequiredService<StoragePaths>();
            Assert.Equal(Path.Combine(restored.Root, "private"), paths.DataDirectory);
            File.Copy(file, paths.DatabaseFile, overwrite: false);
        }
        using var account = await CommerceScenario.OpenAsync(restored);
        var stock = new StockService(account.Factory, account.Actor, test.Clock);
        var batch = Assert.Single((await stock.BatchesAsync()).Items);
        Assert.Equal(3, batch.OnHand); Assert.Equal(2, batch.Reserved); Assert.Equal(1, batch.Quarantined); Assert.Equal(0, batch.Available);
        var ledger = (await stock.MovementsAsync(batch.Id)).Items;
        Assert.Equal(batch.OnHand, ledger.Sum(x => x.PhysicalDelta)); Assert.Equal(batch.Reserved, ledger.Sum(x => x.ReservedDelta));
        var administration = new InventoryAdministration(account.Factory, account.Actor, test.Clock);
        Assert.Equal(1, Assert.Single((await administration.HoldsAsync(batch.Id)).Items).Pending);
        Assert.True((await administration.ReconciliationAsync(batch.Id))!.Consistent);
        var reservation = Assert.Single((await stock.ReservationsAsync(test.OrderId)).Items); Assert.Equal(assignment, reservation.TechnicalAssignmentId);
        var projection = await new OrderTechnicalService(account.Factory, account.Actor, test.Clock).ProjectionAsync(test.OrderId, test.LineId);
        Assert.Equal(1.125m, projection!.Right.Sphere);
        Assert.Equal(hash, SHA256.HashData(await File.ReadAllBytesAsync(file)));
        Assert.Equal(2, Assert.Single((await test.Stock.BatchesAsync()).Items).Reserved);
    }
    [Fact]
    public async Task ReservationAndMovementHistoryArePaginatedWithoutAnArbitraryMaximum()
    {
        await using var test = await InventoryScenario.CreateAsync(quantity: 52); var batch = await test.ReceiveAsync(52);
        for (var i = 0; i < 52; i++) Assert.True((await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batch))).Succeeded);
        var first = await test.Stock.ReservationsAsync(test.OrderId); var second = await test.Stock.ReservationsAsync(test.OrderId, 2);
        Assert.Equal(50, first.Items.Count); Assert.True(first.HasMore); Assert.Equal(2, second.Items.Count); Assert.False(second.HasMore);
        Assert.Empty(first.Items.Select(x => x.Id).Intersect(second.Items.Select(x => x.Id)));
        var ledgerFirst = await test.Stock.MovementsAsync(batch); var ledgerSecond = await test.Stock.MovementsAsync(batch, 2);
        Assert.Equal(50, ledgerFirst.Items.Count); Assert.True(ledgerFirst.HasMore); Assert.Equal(3, ledgerSecond.Items.Count);
        Assert.Equal(52, ledgerFirst.Items.Concat(ledgerSecond.Items).Sum(x => x.PhysicalDelta));
        Assert.Equal(0, Assert.Single((await test.Stock.AvailabilityAsync(test.OrderId)).Items).Unreserved);
    }
}
