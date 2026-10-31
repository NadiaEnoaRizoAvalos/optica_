using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OpticaSosa.Domain.Work;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class CommercialQualityMigrationTests
{
    [Fact]
    public async Task QualityUpgradePreservesHistoricalTypesChecklistsRoundsRolesAndCredentials()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var pro = await ClinicalScenario.OpenAsync(app);
        var service = QualityWorkflowTests.Service(pro);
        var first = await QualityWorkflowTests.CreateAsync(app, pro);
        var customer = (await service.GetAsync(first))!.Work.CustomerId;
        var workIds = new List<Guid> { first };
        foreach (var type in new[] { OpticalWorkType.FrameOnly, OpticalWorkType.Repair })
        {
            var created = await service.CreateAsync(new() { CustomerId = customer, Type = type, Product = "Trabajo histórico ficticio", Frame = "Armazón ficticio" });
            Assert.True(created.Succeeded); workIds.Add(created.Id!.Value);
        }
        foreach (var id in workIds)
        {
            await QualityWorkflowTests.PrepareAsync(service, id);
            foreach (var stage in Enum.GetValues<QualityStage>())
                Assert.True((await service.SaveControlAsync(id, await QualityWorkflowTests.InputAsync(service, id, stage))).Succeeded);
        }
        await using var db = await pro.Factory.CreateDbContextAsync();
        var controls = await SnapshotAsync(db);
        var works = JsonSerializer.Serialize(await db.OpticalWorks.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync());
        var users = await db.Users.OrderBy(x => x.Id).Select(x => new { x.Id, x.PasswordHash, x.SecurityStamp }).ToArrayAsync();
        var audits = await db.AuditEntries.CountAsync();
        // Sólo la base ficticia, con tipos históricos y responsables Profesionales válidos para el esquema anterior.
        await db.GetService<IMigrator>().MigrateAsync("20261018034218_InventoryAdministration");
        Assert.Equal(2, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'trigger' AND name LIKE 'TR_OpticalWorks_Prescription_%'").SingleAsync());
        await db.Database.MigrateAsync(); db.ChangeTracker.Clear();
        Assert.Equal(2, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'trigger' AND name LIKE 'TR_OpticalWorks_Prescription_%'").SingleAsync());
        Assert.Equal(controls, await SnapshotAsync(db));
        Assert.Equal(works, JsonSerializer.Serialize(await db.OpticalWorks.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync()));
        Assert.Equal(users, await db.Users.OrderBy(x => x.Id).Select(x => new { x.Id, x.PasswordHash, x.SecurityStamp }).ToArrayAsync());
        Assert.Equal(audits, await db.AuditEntries.CountAsync());
        foreach (var control in await db.QualityControls.ToListAsync())
            Assert.Equal((await db.OpticalWorks.SingleAsync(x => x.Id == control.WorkId)).Type, control.WorkType);
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal("ok", Assert.Single(await db.Database.SqlQueryRaw<string>("PRAGMA integrity_check").ToListAsync()));
        Assert.Empty(await db.Database.SqlQueryRaw<string>("SELECT 'broken' AS Value FROM pragma_foreign_key_check").ToListAsync());
    }

    [Fact]
    public async Task ClinicalOwnershipTriggersSurviveBothUpgradeAndIsolatedLegacyDowngrade()
    {
        await using var test = await AttachmentTestContext.CreateAsync();
        var service = QualityWorkflowTests.Service(test.Scenario);
        var created = await service.CreateAsync(new() { CustomerId = test.CustomerId, Product = "Trabajo ficticio con receta",
            Frame = "Armazón ficticio", PrescriptionId = test.RecordId });
        Assert.True(created.Succeeded, created.Error);
        using var customers = await CustomerScenario.OpenAsync(test.App);
        var other = (await customers.Customers.CreateAsync(CustomerScenario.Input("FIC-MIG-CALIDAD"))).CustomerId!.Value;
        await using var db = await test.Scenario.Factory.CreateDbContextAsync();
        async Task RejectCrossCustomerAsync() => await Assert.ThrowsAsync<SqliteException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"UPDATE OpticalWorks SET CustomerId = {other} WHERE Id = {created.Id}"));
        await RejectCrossCustomerAsync();
        await db.GetService<IMigrator>().MigrateAsync("20261018034218_InventoryAdministration");
        await RejectCrossCustomerAsync();
        await db.Database.MigrateAsync();
        await RejectCrossCustomerAsync();
        Assert.Equal(test.CustomerId, (await db.OpticalWorks.AsNoTracking().SingleAsync(x => x.Id == created.Id)).CustomerId);
        Assert.Empty(await db.Database.SqlQueryRaw<string>("SELECT 'broken' AS Value FROM pragma_foreign_key_check").ToListAsync());
    }
    private static async Task<string> SnapshotAsync(OpticaSosa.Infrastructure.Persistence.OpticaDbContext db) =>
        JsonSerializer.Serialize(await db.QualityControls.AsNoTracking().Include(x => x.Items).OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.WorkId, x.Round, x.Stage, x.Status, x.ActorId, x.Role, x.AtUtc, x.ApprovedAtUtc,
                x.Notes, x.TemplateVersion, Items = x.Items.OrderBy(i => i.Key).Select(i => new { i.Key, i.Label, i.Applicable, i.Required, i.Status, i.Notes }).ToArray() }).ToArrayAsync());

    [Fact]
    public async Task IncompatibleDowngradeRollsBackWithoutDeletingNewWorkTypesOrApprovals()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var customers = await CustomerScenario.OpenAsync(app);
        var customer = (await customers.Customers.CreateAsync(CustomerScenario.Input())).CustomerId!.Value;
        using var pro = await ClinicalScenario.OpenAsync(app); var service = QualityWorkflowTests.Service(pro);
        var created = await service.CreateAsync(new() { CustomerId = customer, Type = OpticalWorkType.ContactLenses, Product = "Contactos ficticios" });
        Assert.True(created.Succeeded); var id = created.Id!.Value;
        await QualityWorkflowTests.PrepareAsync(service, id);
        Assert.True((await service.SaveControlAsync(id, await QualityWorkflowTests.InputAsync(service, id, QualityStage.Technical))).Succeeded);
        await using (var attempt = await pro.Factory.CreateDbContextAsync())
            await Assert.ThrowsAsync<SqliteException>(() => attempt.GetService<IMigrator>().MigrateAsync("20261018034218_InventoryAdministration"));
        await using var db = await pro.Factory.CreateDbContextAsync();
        Assert.Equal(OpticalWorkType.ContactLenses, (await db.OpticalWorks.SingleAsync(x => x.Id == id)).Type);
        // Tras descender migraciones posteriores vacías, consultar campos del esquema 10.1, sin columnas futuras.
        var approval = Assert.Single(await db.QualityControls.Where(x => x.WorkId == id)
            .Select(x => new { x.Stage, x.Status, x.Role }).ToListAsync());
        Assert.Equal(QualityStage.Technical, approval.Stage); Assert.Equal(ControlStatus.Approved, approval.Status);
        Assert.Equal(ResponsibleRole.Professional, approval.Role);
        Assert.Contains("20261018040139_ProductQualityMatrix", await db.Database.GetAppliedMigrationsAsync());
        Assert.Equal(1, Assert.Single(await db.Database.SqlQueryRaw<int>("PRAGMA foreign_keys").ToListAsync()));
        Assert.Equal(2, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'trigger' AND name LIKE 'TR_OpticalWorks_Prescription_%'").SingleAsync());
        Assert.Equal("ok", Assert.Single(await db.Database.SqlQueryRaw<string>("PRAGMA integrity_check").ToListAsync()));
    }
}
