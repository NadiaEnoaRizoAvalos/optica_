using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using OpticaSosa.Infrastructure.Persistence.Migrations;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class SupplierMigrationTests
{
    [Fact]
    public async Task AdditiveMigrationPreservesClinicalHistoryRolesAndCredentials()
    {
        var migration = new Suppliers();
        Assert.All(migration.UpOperations, operation => Assert.True(operation is CreateTableOperation or CreateIndexOperation));
        Assert.Equal("Suppliers", Assert.Single(migration.UpOperations.OfType<CreateTableOperation>()).Name);
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var customers = await CustomerScenario.OpenAsync(app);
        var customer = (await customers.Customers.CreateAsync(CustomerScenario.Input())).CustomerId!.Value;
        using var clinical = await ClinicalScenario.OpenAsync(app);
        var professional = await clinical.AddProfessionalAsync();
        var record = await clinical.Clinical.CreateAsync(customer, ClinicalScenario.Input(professional));
        Assert.True(record.Succeeded);
        await using var db = await clinical.Factory.CreateDbContextAsync();
        var usersBefore = await db.Users.OrderBy(x => x.Id).Select(x => new { x.Id, x.PasswordHash, x.SecurityStamp }).ToArrayAsync();
        var rolesBefore = await db.UserRoles.OrderBy(x => x.UserId).Select(x => new { x.UserId, x.RoleId }).ToArrayAsync();
        var auditsBefore = await db.AuditEntries.CountAsync();
        // Sólo esta base ficticia, sin proveedores: volver al esquema previo y ensayar el avance.
        await db.GetService<IMigrator>().MigrateAsync("20261016161235_BackupDestinationBinding");
        await db.Database.MigrateAsync();
        Assert.Equal(usersBefore, await db.Users.OrderBy(x => x.Id).Select(x => new { x.Id, x.PasswordHash, x.SecurityStamp }).ToArrayAsync());
        Assert.Equal(rolesBefore, await db.UserRoles.OrderBy(x => x.UserId).Select(x => new { x.UserId, x.RoleId }).ToArrayAsync());
        Assert.Equal(auditsBefore, await db.AuditEntries.CountAsync());
        Assert.Equal(customer, (await db.Customers.SingleAsync()).Id);
        Assert.Equal(professional, (await db.Professionals.SingleAsync()).Id);
        Assert.Equal(record.Id, (await db.ClinicalRecords.SingleAsync()).Id);
        Assert.Equal(1.125m, (await clinical.Clinical.GetAsync(customer, record.Id!.Value))!.Data.Right.Sphere);
        Assert.Empty(await db.Suppliers.ToListAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal("ok", Assert.Single(await db.Database.SqlQueryRaw<string>("PRAGMA integrity_check").ToListAsync()));
    }
}
