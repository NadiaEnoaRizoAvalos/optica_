using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Application.Security;
using OpticaSosa.Infrastructure.Identity;
using OpticaSosa.Infrastructure.Persistence;
using OpticaSosa.Infrastructure.Backups;
using OpticaSosa.Infrastructure.Clinical;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class BackupMigrationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ForwardMigrationAndIsolatedRecoveryPreservePriorData(bool stopAfterFirstMigration, bool useSqlScript)
    {
        await using var app = new SecurityApp(); using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpticaDbContext>();
        var paths = scope.ServiceProvider.GetRequiredService<StoragePaths>();
        Assert.Equal(Path.Combine(app.Root, "private"), paths.DataDirectory);
        await db.GetService<IMigrator>().MigrateAsync("20261016035916_QualityWorkflow");
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All) Assert.True((await roles.CreateAsync(new(role))).Succeeded);
        Assert.True((await scope.ServiceProvider.GetRequiredService<LocalProvisioning>().CreateFirstAdminAsync("admin.ficticio", SecurityApp.TestPassword)).Succeeded);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var professional = new ApplicationUser { UserName = "profesional.ficticio" };
        Assert.True((await users.CreateAsync(professional, SecurityApp.TestPassword)).Succeeded);
        Assert.True((await users.AddToRoleAsync(professional, Roles.Professional)).Succeeded);
        using var customers = await CustomerScenario.OpenAsync(app);
        var customer = (await customers.Customers.CreateAsync(CustomerScenario.Input())).CustomerId!.Value;
        using var pro = await ClinicalScenario.OpenAsync(app);
        var record = await pro.Clinical.CreateAsync(customer, ClinicalScenario.Input(await pro.AddProfessionalAsync()));
        Assert.True(record.Succeeded);
        var documents = new[] { (BusinessForms.Logo(), "receta-ficticia.png", "image/png"),
            (AttachmentTestContext.Pdf(), "receta-ficticia.pdf", "application/pdf") };
        foreach (var (bytes, name, type) in documents)
        {
            var detail = (await pro.Clinical.GetAsync(customer, record.Id!.Value))!;
            using var input = new MemoryStream(bytes);
            Assert.True((await pro.Attachments.UploadAsync(customer, record.Id.Value,
                new(Guid.NewGuid(), detail.RevisionId, detail.Version, name, type, bytes.Length, input))).Succeeded);
        }
        var before = await RowsAsync(db);
        var original = Path.Combine(paths.AttachmentsDirectory, "original-ficticio.txt");
        await File.WriteAllTextAsync(original, "Original ficticio conservado");
        var coordinator = app.Services.GetRequiredService<BackupCoordinator>();
        var verifier = app.Services.GetRequiredService<BackupVerifier>();
        var snapshot = await app.Services.GetRequiredService<ClinicalSnapshotWriter>()
            .CreateAsync(Path.Combine(paths.DataDirectory, "migration-rehearsal"));
        await verifier.VerifyAsync(snapshot.Directory);
        var snapshotHash = SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(snapshot.Directory, "optica.db")));
        Assert.False(await coordinator.ReadyAsync(default));
        if (useSqlScript)
        {
            var script = db.GetService<IMigrator>().GenerateScript("20261016035916_QualityWorkflow",
                "20261016161235_BackupDestinationBinding");
            await db.Database.ExecuteSqlRawAsync(script);
        }
        else if (stopAfterFirstMigration)
            await db.GetService<IMigrator>().MigrateAsync("20261016055719_AutomaticBackups");
        // Este ensayo verifica exclusivamente las dos migraciones de respaldos de fase 5.2.A.
        // Las migraciones posteriores tienen sus propias pruebas de compatibilidad.
        else await db.GetService<IMigrator>().MigrateAsync("20261016161235_BackupDestinationBinding");
        var after = await RowsAsync(db);
        Assert.All(before, pair => Assert.Equal(pair.Value, after[pair.Key]));
        Assert.Equal(before.Count + 2, after.Count);
        Assert.Equal("Original ficticio conservado", await File.ReadAllTextAsync(original));
        Assert.Equal(0, await db.BackupSettings.CountAsync()); Assert.Equal(0, await db.BackupRuns.CountAsync());
        Assert.Equal(!stopAfterFirstMigration, await coordinator.ReadyAsync(default));

        // Recuperación hacia un directorio nuevo: no ejecutar Down ni reemplazar la base ensayada.
        var recovered = await verifier.RestoreIsolatedAsync(snapshot.Directory);
        Assert.NotEqual(paths.DataDirectory, recovered);
        var options = new DbContextOptionsBuilder<OpticaDbContext>().UseSqlite(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            { DataSource = Path.Combine(recovered, "optica.db"), Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly, Pooling = false }.ToString()).Options;
        await using var restored = new OpticaDbContext(options);
        var recoveredRows = await RowsAsync(restored);
        Assert.Equal(before.Count, recoveredRows.Count);
        Assert.All(before, pair => Assert.Equal(pair.Value, recoveredRows[pair.Key]));
        Assert.Equal("20261016035916_QualityWorkflow", (await restored.Database.GetAppliedMigrationsAsync()).Last());
        Assert.Equal(snapshotHash, SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(recovered, "optica.db"))));
        Assert.Equal(snapshotHash, SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(snapshot.Directory, "optica.db"))));
        Assert.Equal("Original ficticio conservado", await File.ReadAllTextAsync(Path.Combine(recovered, "attachments", "original-ficticio.txt")));
        Assert.Equal(2, await restored.ClinicalAttachments.CountAsync());
        await verifier.VerifyAsync(recovered);
    }

    private static async Task<Dictionary<string, string>> RowsAsync(OpticaDbContext db)
    {
        await db.Database.OpenConnectionAsync(); var connection = db.Database.GetDbConnection();
        var tables = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name NOT LIKE '__EF%'";
            await using var reader = await command.ExecuteReaderAsync(); while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
        }
        var result = new Dictionary<string, string>();
        foreach (var table in tables)
        {
            await using var command = connection.CreateCommand(); command.CommandText = "SELECT * FROM \"" + table.Replace("\"", "\"\"") + "\"";
            await using var reader = await command.ExecuteReaderAsync(); var rows = new List<string>();
            while (await reader.ReadAsync())
            {
                var values = new object[reader.FieldCount]; reader.GetValues(values);
                rows.Add(System.Text.Json.JsonSerializer.Serialize(values));
            }
            result[table] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", rows.Order(StringComparer.Ordinal)))));
        }
        return result;
    }
}
