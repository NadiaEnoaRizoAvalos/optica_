using System.Net;
using System.Security.Cryptography;
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

public sealed class BackupNavigationTests
{
    [Fact]
    public async Task AdminCanReachBackupsFromMenuAndClinicalStorageRemainsAvailable()
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync();
        using var browser = test.App.Browser(); await BrowserForms.LoginAsync(browser, "admin.ficticio");
        var administration = WebUtility.HtmlDecode(await browser.GetStringAsync("/administracion"));
        Assert.Contains("href=\"/admin/respaldos\"", administration);
        Assert.Contains("href=\"/admin/archivos-clinicos\"", administration);
        var clinical = WebUtility.HtmlDecode(await browser.GetStringAsync("/admin/archivos-clinicos"));
        Assert.Contains("Almacenamiento clínico", clinical); Assert.Contains("Crear copia local verificada", clinical);
        Assert.Contains("href=\"/admin/respaldos\"", clinical); Assert.DoesNotContain("fase 7", clinical);
        Assert.Contains("fase 5.2.A", clinical);
        foreach (var name in new[] { "ventas.ficticio", "profesional.ficticio" })
        {
            using var other = test.App.Browser(); await BrowserForms.LoginAsync(other, name);
            Assert.DoesNotContain("href=\"/admin/respaldos\"", await other.GetStringAsync("/"));
        }
    }

    [Fact]
    public async Task PreviousSchemaShowsDisabledConfigurationWithoutMigratingOrWritingData()
    {
        await using var app = new SecurityApp(); using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpticaDbContext>();
        var paths = scope.ServiceProvider.GetRequiredService<StoragePaths>();
        Assert.Equal(Path.Combine(app.Root, "private"), paths.DataDirectory);
        const string previous = "20261016035916_QualityWorkflow";
        await db.GetService<IMigrator>().MigrateAsync(previous);
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True((await roles.CreateAsync(new IdentityRole(Roles.Administrator))).Succeeded);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = new ApplicationUser { UserName = "admin.ficticio" };
        Assert.True((await users.CreateAsync(admin, SecurityApp.TestPassword)).Succeeded);
        Assert.True((await users.AddToRoleAsync(admin, Roles.Administrator)).Succeeded);
        using var browser = app.Browser(); await BrowserForms.LoginAsync(browser, "admin.ficticio");
        var before = SHA256.HashData(await File.ReadAllBytesAsync(paths.DatabaseFile));
        var response = await browser.GetAsync("/admin/respaldos");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Pantalla nueva de respaldos — fase 5.2.A", html);
        Assert.Contains("<fieldset disabled=\"disabled\">", html);
        Assert.Contains("Hora local", html); Assert.Contains("Frecuencia", html);
        Assert.Contains("Disco externo configurado:", html); Assert.Contains("Estado de conexión:", html);
        Assert.Contains("Últimas ejecuciones", html); Assert.DoesNotContain("value=\"20:00\"", html);
        var attempted = await BrowserForms.PostAsync(browser, "/admin/respaldos", "/admin/respaldos?handler=Save",
            new() { ["Enabled"] = "true", ["EveryDays"] = "1", ["KeepCopies"] = "3", ["LocalTime"] = "06:45",
                ["TimeZoneId"] = "UTC", ["RelativeFolder"] = "Respaldos", ["Version"] = Guid.NewGuid().ToString() });
        Assert.Equal(HttpStatusCode.OK, attempted.StatusCode);
        Assert.Equal(before, SHA256.HashData(await File.ReadAllBytesAsync(paths.DatabaseFile)));
        Assert.Equal(previous, (await db.Database.GetAppliedMigrationsAsync()).Last());
        Assert.Contains("href=\"/admin/respaldos\"", await browser.GetStringAsync("/admin/archivos-clinicos"));
    }
}
