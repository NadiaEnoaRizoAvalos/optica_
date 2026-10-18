using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Infrastructure.Persistence.Migrations;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class InventoryAdministrationMigrationTests
{
    [Fact]
    public async Task UpgradePreservesPreviousQuarantineLedgerClinicalHistoryAndCredentials()
    {
        var migration = new InventoryAdministration();
        Assert.Equal(5, migration.UpOperations.OfType<CreateTableOperation>().Count());
        Assert.All(migration.UpOperations, x => Assert.True(x is CreateTableOperation or CreateIndexOperation
            or AddCheckConstraintOperation or DropCheckConstraintOperation or SqlOperation));
        Assert.All(migration.UpOperations.OfType<DropCheckConstraintOperation>(), x => Assert.Equal("StockMovements", x.Table));
        await using var test = await InventoryScenario.CreateAsync();
        await using var db = await test.Sales.Factory.CreateDbContextAsync();
        var users = await db.Users.OrderBy(x => x.Id).Select(x => new { x.Id, x.PasswordHash, x.SecurityStamp }).ToArrayAsync();
        var roles = await db.UserRoles.OrderBy(x => x.UserId).ThenBy(x => x.RoleId).Select(x => new { x.UserId, x.RoleId }).ToArrayAsync();
        var clinical = await db.ClinicalRevisions.Select(x => x.Id).Order().ToArrayAsync();
        var audits = await db.AuditEntries.CountAsync();
        // Exclusivamente la base ficticia de SecurityApp; todavía no tiene movimientos ni tablas nuevas con datos.
        await db.GetService<IMigrator>().MigrateAsync("20261018031522_LaboratoryOrders");
        var variant = await db.ProductVariants.SingleAsync(x => x.Id == test.VariantId);
        var batch = new StockBatch(Guid.NewGuid(), variant, 3, 1, null, null, ExpiryPrecision.None,
            test.Sales.Actor.Id!, test.Clock.GetUtcNow().UtcDateTime);
        db.StockBatches.Add(batch);
        var movement = new StockMovement(Guid.NewGuid(), batch.Id, null, StockMovementKind.Receipt,
            3, 0, 1, "Recepción ficticia anterior", new string('0', 64), test.Sales.Actor.Id!, test.Clock.GetUtcNow().UtcDateTime);
        db.StockMovements.Add(movement); await db.SaveChangesAsync();
        await db.Database.MigrateAsync(); db.ChangeTracker.Clear();
        var restored = await db.StockBatches.SingleAsync(x => x.Id == batch.Id);
        Assert.Equal(3, restored.OnHand); Assert.Equal(1, restored.Quarantined); Assert.Equal(batch.Version, restored.Version);
        var old = await db.StockMovements.SingleAsync(); Assert.Equal(movement.Id, old.Id); Assert.Equal(3, old.PhysicalDelta);
        var hold = await db.StockHolds.SingleAsync(); Assert.Equal(batch.Id, hold.BatchId); Assert.Equal(1, hold.PendingQuantity);
        Assert.Equal(users, await db.Users.OrderBy(x => x.Id).Select(x => new { x.Id, x.PasswordHash, x.SecurityStamp }).ToArrayAsync());
        Assert.Equal(roles, await db.UserRoles.OrderBy(x => x.UserId).ThenBy(x => x.RoleId).Select(x => new { x.UserId, x.RoleId }).ToArrayAsync());
        Assert.Equal(clinical, await db.ClinicalRevisions.Select(x => x.Id).Order().ToArrayAsync());
        Assert.Equal(audits, await db.AuditEntries.CountAsync());
        var service = new OpticaSosa.Infrastructure.Inventory.InventoryAdministration(test.Sales.Factory, test.Sales.Actor, test.Clock);
        Assert.True((await service.ReconciliationAsync(batch.Id))!.Consistent);
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal("ok", Assert.Single(await db.Database.SqlQueryRaw<string>("PRAGMA integrity_check").ToListAsync()));
        Assert.Empty(await db.Database.SqlQueryRaw<string>("SELECT 'broken' AS Value FROM pragma_foreign_key_check").ToListAsync());
    }
}
