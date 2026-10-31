using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Domain.Delivery;
using OpticaSosa.Domain.Work;
using OpticaSosa.Infrastructure.Delivery;
using OpticaSosa.Infrastructure.Inventory;
using OpticaSosa.Infrastructure.Persistence;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class OrderDeliveryRecoveryTests
{
    [Fact]
    public async Task ExistingBackupRecoversDeliveredOrderQualityMaterialsProofsAndBalancesWithoutChangingOriginal()
    {
        await using var test = await InventoryScenario.CreateAsync(); var delivery = new DeliveryScenario(test);
        var batch = await test.ReceiveAsync(); Assert.True((await test.Stock.ReserveAsync(test.OrderId, test.LineId, await test.ReserveInputAsync(batch))).Succeeded);
        var work = await delivery.GroupAsync(); await delivery.PayAsync(); await delivery.ApproveAsync(work);
        Assert.True((await delivery.Fulfilment.DeliverAsync(test.OrderId, await delivery.DeliveryAsync())).Succeeded);
        using var administrator = await ClinicalScenario.OpenAsync(test.Test.App, "admin.ficticio");
        var snapshot = await administrator.Snapshots.CreateAsync(); Assert.Equal("Complete", snapshot.Status);
        var file = Path.Combine(snapshot.Directory, "optica.db"); var hash = SHA256.HashData(await File.ReadAllBytesAsync(file));
        await using var restored = new SecurityApp();
        using (var scope = restored.Services.CreateScope())
        {
            var paths = scope.ServiceProvider.GetRequiredService<StoragePaths>();
            Assert.Equal(Path.Combine(restored.Root, "private"), paths.DataDirectory);
            File.Copy(file, paths.DatabaseFile, overwrite: false);
        }
        using var account = await CommerceScenario.OpenAsync(restored);
        var recovered = new OrderFulfilmentService(account.Factory, account.Actor, test.Clock);
        Assert.Equal(OpticaSosa.Application.Delivery.FulfilmentState.Delivered, (await recovered.GetAsync(test.OrderId))!.State);
        await using var db = await account.Factory.CreateDbContextAsync();
        Assert.Single(await db.StockDeliveryProofs.ToListAsync());
        Assert.Equal(2, await db.QualityControls.CountAsync(x => x.WorkId == work && x.MaterialHash != null));
        Assert.Equal(0, (await account.Orders.GetAsync(test.OrderId))!.BalanceMinor);
        Assert.True((await new InventoryAdministration(account.Factory, account.Actor, test.Clock).ReconciliationAsync(batch))!.Consistent);
        Assert.Equal(hash, SHA256.HashData(await File.ReadAllBytesAsync(file)));
        Assert.Equal("ok", Assert.Single(await db.Database.SqlQueryRaw<string>("PRAGMA integrity_check").ToListAsync()));
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => db.GetService<IMigrator>().MigrateAsync("20261018040139_ProductQualityMatrix"));
        await using var stillCurrent = await account.Factory.CreateDbContextAsync();
        Assert.Single(await stillCurrent.Set<OrderDelivery>().ToListAsync());
        Assert.Equal(2, await stillCurrent.QualityControls.CountAsync(x => x.WorkId == work && x.MaterialHash != null));
        Assert.Equal("20261031000517_QualityMaterialEvidence", (await stillCurrent.Database.GetAppliedMigrationsAsync()).Last());
    }

    [Fact]
    public async Task AdditiveMigrationKeepsExistingWorksRolesClinicalVersionsAndAudit()
    {
        await using var test = await InventoryScenario.CreateAsync(); var helper = new DeliveryScenario(test);
        var created = await helper.ProWork.CreateAsync(new() { CustomerId = test.Test.CustomerId, Product = "Trabajo histórico ficticio", Frame = "Armazón ficticio" });
        Assert.True(created.Succeeded); var work = created.Id!.Value;
        await QualityWorkflowTests.PrepareAsync(helper.ProWork, work);
        Assert.True((await helper.ProWork.SaveControlAsync(work, await QualityWorkflowTests.InputAsync(helper.ProWork, work, QualityStage.Technical))).Succeeded);
        await using var db = await test.Sales.Factory.CreateDbContextAsync();
        var users = await db.Users.OrderBy(x => x.Id).Select(x => new { x.Id, x.PasswordHash, x.SecurityStamp }).ToArrayAsync();
        var clinical = await db.ClinicalRevisions.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync();
        var controls = await db.QualityControls.OrderBy(x => x.Id).Select(x => new { x.Id, x.Stage, x.Role, x.Round, x.ApprovedAtUtc }).ToArrayAsync();
        var version = (await db.OpticalWorks.SingleAsync(x => x.Id == work)).Version;
        var audits = await db.AuditEntries.CountAsync();
        // Sólo esta base ficticia: las tablas nuevas están vacías y los campos nuevos del trabajo histórico son nulos.
        await db.GetService<IMigrator>().MigrateAsync("20261018040139_ProductQualityMatrix");
        await db.Database.MigrateAsync(); db.ChangeTracker.Clear();
        Assert.Equal(users, await db.Users.OrderBy(x => x.Id).Select(x => new { x.Id, x.PasswordHash, x.SecurityStamp }).ToArrayAsync());
        Assert.Equal(clinical, await db.ClinicalRevisions.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync());
        Assert.Equal(controls, await db.QualityControls.OrderBy(x => x.Id).Select(x => new { x.Id, x.Stage, x.Role, x.Round, x.ApprovedAtUtc }).ToArrayAsync());
        Assert.Equal(version, (await db.OpticalWorks.SingleAsync(x => x.Id == work)).Version); Assert.Equal(audits, await db.AuditEntries.CountAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Empty(await db.Set<CommercialWork>().ToListAsync());
        Assert.Equal(2, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='trigger' AND name LIKE 'TR_OpticalWorks_Prescription_%'").SingleAsync());
        Assert.Empty(await db.Database.SqlQueryRaw<string>("SELECT 'broken' AS Value FROM pragma_foreign_key_check").ToListAsync());
    }
}
