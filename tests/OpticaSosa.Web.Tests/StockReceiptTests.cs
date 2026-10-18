using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Commerce;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Infrastructure.Inventory;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class StockReceiptTests
{
    [Fact]
    public async Task ReceiptsAreIdempotentAndTheLedgerReconcilesWithPhysicalAndQuarantinedCounts()
    {
        await using var test = await InventoryScenario.CreateAsync(); var input = await test.ReceiptAsync(3, 1);
        var result = await test.Stock.ReceiveAsync(input); Assert.True(result.Succeeded, result.Error);
        Assert.True((await test.Stock.ReceiveAsync(input)).Succeeded);
        Assert.True((await test.Stock.ReceiveAsync(input with { Quantity = 4 })).Conflict);
        var batch = Assert.Single((await test.Stock.BatchesAsync()).Items);
        var movement = Assert.Single((await test.Stock.MovementsAsync(batch.Id)).Items);
        Assert.Equal(batch.OnHand, movement.PhysicalDelta); Assert.Equal(batch.Quarantined, movement.QuarantineDelta);
        Assert.Equal(2, batch.Available);
        await using var db = await test.Sales.Factory.CreateDbContextAsync();
        Assert.Single(await db.AuditEntries.Where(x => x.Action == "inventory.received").ToListAsync());
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE StockBatches SET Reserved = 4 WHERE Id = {batch.Id}"));
        Assert.Equal(0, Assert.Single((await test.Stock.BatchesAsync()).Items).Reserved);
    }
    [Fact]
    public async Task ContactsKeepLotAndMonthPrecisionAndRejectConflictingExpiryForTheSameLot()
    {
        await using var test = await InventoryScenario.CreateAsync(ProductCategory.ContactLens);
        Assert.False((await test.Stock.ReceiveAsync(await test.ReceiptAsync())).Succeeded);
        var batchId = await test.ReceiveAsync(2, lot: "LOTE-FIC", expiry: new(2026, 10, 1), precision: ExpiryPrecision.Month);
        var batch = Assert.Single((await test.Stock.BatchesAsync()).Items);
        Assert.Equal(new DateOnly(2026, 10, 31), batch.ExpiresOn); Assert.Equal(ExpiryPrecision.Month, batch.ExpiryPrecision);
        Assert.False((await test.Stock.ReceiveAsync(await test.ReceiptAsync(lot: " lote-fic ", expiry: new(2026, 11, 30), precision: ExpiryPrecision.Month))).Succeeded);
        Assert.Equal(batchId, Assert.Single((await test.Stock.BatchesAsync()).Items).Id);
    }
    [Fact]
    public async Task LastDayIsUsableButTheFollowingDayBlocksNewReservationsWithoutErasingExistingOnes()
    {
        await using var test = await InventoryScenario.CreateAsync(ProductCategory.ContactLens, quantity: 2);
        var batchId = await test.ReceiveAsync(2, lot: "LOTE-FIC", expiry: new(2026, 10, 17), precision: ExpiryPrecision.Day);
        var assignment = await test.AssignAsync();
        test.Clock.Zone = TimeZoneInfo.CreateCustomTimeZone("Ficticia-UTC-3", TimeSpan.FromHours(-3), "Ficticia UTC-3", "Ficticia UTC-3");
        test.Clock.Now = new(2026, 10, 18, 1, 0, 0, TimeSpan.Zero); // Localmente todavía es el último día válido.
        Assert.True((await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batchId, assignment: assignment))).Succeeded);
        test.Clock.Now = test.Clock.Now.AddHours(3);
        Assert.Equal(SalesResultCode.Invalid, (await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batchId, assignment: assignment))).Code);
        var batch = Assert.Single((await test.Stock.BatchesAsync()).Items);
        Assert.Equal(2, batch.OnHand); Assert.Equal(1, batch.Reserved); Assert.Equal(0, batch.Available);
        Assert.Equal(ReservationStatus.Active, Assert.Single((await test.Stock.ReservationsAsync(test.OrderId)).Items).Status);
    }
    [Fact]
    public async Task ProfessionalCanReadStockButCannotReceiveOrReserveItThroughFinancialCommands()
    {
        await using var test = await InventoryScenario.CreateAsync(); var batch = await test.ReceiveAsync();
        var professional = new StockService(test.Sales.Factory, test.Test.Clinical.Actor, test.Clock);
        Assert.Single((await professional.BatchesAsync()).Items);
        Assert.Single((await professional.MovementsAsync(batch)).Items);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await professional.ReceiveAsync(await test.ReceiptAsync()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await professional.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batch)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => professional.AvailabilityAsync(test.OrderId));
    }
    [Fact]
    public async Task AuditFailureRollsBackTheReceiptAndAllPhysicalCounts()
    {
        await using var test = await InventoryScenario.CreateAsync();
        await using var db = await test.Sales.Factory.CreateDbContextAsync();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectFictitiousStockAudit BEFORE INSERT ON AuditEntries WHEN NEW.Action = 'inventory.received' BEGIN SELECT RAISE(ABORT, 'Fictitious audit failure'); END;");
        Assert.False((await test.Stock.ReceiveAsync(await test.ReceiptAsync(2))).Succeeded);
        Assert.Empty(await db.StockBatches.ToListAsync()); Assert.Empty(await db.StockMovements.ToListAsync());
    }
}
