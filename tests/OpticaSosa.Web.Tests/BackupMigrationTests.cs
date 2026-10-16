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
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class BackupMigrationTests
{
    [Fact]
    public async Task ForwardMigrationPreservesEveryPriorTableAndPrivateOriginal()
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
        Assert.True((await pro.Clinical.CreateAsync(customer, ClinicalScenario.Input(await pro.AddProfessionalAsync()))).Succeeded);
        var before = await RowsAsync(db);
        var original = Path.Combine(paths.AttachmentsDirectory, "original-ficticio.txt");
        await File.WriteAllTextAsync(original, "Original ficticio conservado");
        Assert.False(await app.Services.GetRequiredService<OpticaSosa.Infrastructure.Backups.BackupCoordinator>().ReadyAsync(default));
        await db.Database.MigrateAsync();
        var after = await RowsAsync(db);
        Assert.All(before, pair => Assert.Equal(pair.Value, after[pair.Key]));
        Assert.Equal(before.Count + 2, after.Count);
        Assert.Equal("Original ficticio conservado", await File.ReadAllTextAsync(original));
        Assert.Equal(0, await db.BackupSettings.CountAsync()); Assert.Equal(0, await db.BackupRuns.CountAsync());
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
