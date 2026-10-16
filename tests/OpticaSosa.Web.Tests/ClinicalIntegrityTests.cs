using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Domain.Clinical;
using OpticaSosa.Infrastructure.Persistence;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class ClinicalIntegrityTests
{
    [Fact]
    public async Task DatabaseRejectsDuplicateEyesAndCrossCustomerLinksEvenOutsideTheService()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var customers = await CustomerScenario.OpenAsync(app);
        var first = (await customers.Customers.CreateAsync(CustomerScenario.Input("FIC-A"))).CustomerId!.Value;
        var second = (await customers.Customers.CreateAsync(CustomerScenario.Input("FIC-B"))).CustomerId!.Value;
        var details = (await customers.Customers.GetAsync(first))!;
        await customers.Visits.StartAsync(first, new() { CustomerVersion = details.Version, Reason = "Visita ficticia" });
        var visit = (await customers.Customers.GetAsync(first))!.Visits.Single().Id;
        using var scenario = await ClinicalScenario.OpenAsync(app);
        var professional = await scenario.AddProfessionalAsync();
        var input = ClinicalScenario.Input(professional);
        Assert.True((await scenario.Clinical.CreateAsync(first, input)).Succeeded);
        await using (var db = await scenario.Factory.CreateDbContextAsync())
        {
            db.EyeMeasurements.Add(new(input.SubmissionId, EyeSide.Right, new()));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using (var db = await scenario.Factory.CreateDbContextAsync())
        {
            var invalid = new ClinicalRecord(Guid.NewGuid(), second, visit, ClinicalKind.MedicalPrescription);
            invalid.AddRevision(); db.ClinicalRecords.Add(invalid);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using (var db = await scenario.Factory.CreateDbContextAsync())
        {
            var row = await db.Professionals.SingleAsync(); db.Remove(row);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task UpgradePreservesCustomersBusinessCredentialsAuditAndPrivateFiles()
    {
        await using var app = new SecurityApp();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpticaDbContext>();
        // Sólo una base vacía y aislada se prepara en el esquema anterior. Nunca se usa Down.
        await db.GetService<IMigrator>().MigrateAsync("20261016010809_BusinessConfiguration");
        // Inicializar cuentas en el esquema previo sin adelantar la migración clínica.
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<OpticaSosa.Infrastructure.Identity.ApplicationUser>>();
        var user = new OpticaSosa.Infrastructure.Identity.ApplicationUser { UserName = "previo.ficticio" };
        Assert.True((await users.CreateAsync(user, SecurityApp.TestPassword)).Succeeded);
        db.Customers.Add(new(Guid.NewGuid(), new("Cliente ficticio", "Prueba", "FIC-PREVIO", null, null), user.Id, DateTime.UtcNow));
        db.BusinessSettings.Add(new());
        db.AuditEntries.Add(new() { Action = "synthetic.previous", ActorId = user.Id });
        await db.SaveChangesAsync();
        var originalHash = user.PasswordHash;
        var storage = scope.ServiceProvider.GetRequiredService<StoragePaths>();
        Assert.Equal(Path.Combine(app.Root, "private"), storage.DataDirectory);
        var attachment = Path.Combine(storage.AttachmentsDirectory, "synthetic.txt");
        await File.WriteAllTextAsync(attachment, "Contenido ficticio");
        await db.Database.MigrateAsync(); db.ChangeTracker.Clear();
        Assert.Equal(originalHash, (await db.Users.SingleAsync()).PasswordHash);
        Assert.Single(await db.Customers.ToListAsync()); Assert.Single(await db.BusinessSettings.ToListAsync());
        Assert.True(await db.AuditEntries.AnyAsync(x => x.Action == "synthetic.previous"));
        Assert.Equal("Contenido ficticio", await File.ReadAllTextAsync(attachment));
        Assert.Empty(await db.ClinicalRecords.ToListAsync()); Assert.Empty(await db.Professionals.ToListAsync());
    }
}
