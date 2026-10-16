using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Infrastructure.Identity;
using OpticaSosa.Infrastructure.Persistence;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class AuthenticationTests
{
    [Fact]
    public async Task AnonymousRequestsAreChallengedAndLoginRequiresCsrf()
    {
        await using var app = new SecurityApp();
        await app.InitializeAsync();
        using var client = app.Browser();
        foreach (var path in new[] { "/", "/admin/usuarios", "/admin/auditoria" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/cuenta/ingresar", response.Headers.Location!.ToString());
        }
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/app.css")).StatusCode);
        var invalid = await client.PostAsync("/cuenta/ingresar", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["UserName"] = "admin.ficticio", ["Password"] = SecurityApp.TestPassword }));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task LoginLogoutAndHashStorageWorkWithoutPublicRegistration()
    {
        await using var app = new SecurityApp();
        await app.InitializeAsync();
        using var client = app.Browser();
        var login = await BrowserForms.LoginAsync(client, "admin.ficticio");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var cookie = login.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("OpticaSosa.Session="));
        Assert.Contains("httponly", cookie.ToLowerInvariant());
        Assert.Contains("samesite=strict", cookie.ToLowerInvariant());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/admin/usuarios")).StatusCode);
        var logout = await BrowserForms.PostAsync(client, "/", "/cuenta/salir", new());
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/admin/usuarios")).StatusCode);
        using var scope = app.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await manager.FindByNameAsync("admin.ficticio");
        Assert.NotEqual(SecurityApp.TestPassword, user!.PasswordHash);
        Assert.True(await manager.CheckPasswordAsync(user, SecurityApp.TestPassword));
        Assert.False((await scope.ServiceProvider.GetRequiredService<LocalProvisioning>()
            .CreateFirstAdminAsync("otro.ficticio", SecurityApp.TestPassword)).Succeeded);
        var events = await scope.ServiceProvider.GetRequiredService<OpticaDbContext>().AuditEntries.ToListAsync();
        Assert.Contains(events, x => x.Action == "login.succeeded");
        Assert.Contains(events, x => x.Action == "logout");
        Assert.DoesNotContain(events, x => x.Action.Contains(SecurityApp.TestPassword));
    }

    [Fact]
    public async Task RepeatedFailuresLockTheAccountAndLocalRecoveryRevokesOldCookies()
    {
        await using var app = new SecurityApp();
        await app.InitializeAsync();
        using var oldSession = app.Browser();
        Assert.Equal(HttpStatusCode.Redirect, (await BrowserForms.LoginAsync(oldSession, "admin.ficticio")).StatusCode);
        using var attacker = app.Browser();
        for (var attempt = 0; attempt < 5; attempt++)
            Assert.Equal(HttpStatusCode.OK, (await BrowserForms.LoginAsync(attacker, "admin.ficticio", "Incorrecta-123!")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await BrowserForms.LoginAsync(attacker, "admin.ficticio")).StatusCode);
        using (var scope = app.Services.CreateScope())
        {
            var provisioning = scope.ServiceProvider.GetRequiredService<LocalProvisioning>();
            Assert.True((await provisioning.RecoverAdminAsync("admin.ficticio", "Recuperada-Ficticia-482!")).Succeeded);
        }
        Assert.Equal(HttpStatusCode.Redirect, (await oldSession.GetAsync("/admin/usuarios")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await BrowserForms.LoginAsync(attacker, "admin.ficticio", "Recuperada-Ficticia-482!")).StatusCode);
    }

    [Fact]
    public async Task ChangingOwnPasswordRequiresCurrentPasswordAndRevokesOtherSessions()
    {
        await using var app = new SecurityApp();
        await app.InitializeAsync();
        using var browser = app.Browser();
        using var other = app.Browser();
        await BrowserForms.LoginAsync(browser, "ventas.ficticio");
        await BrowserForms.LoginAsync(other, "ventas.ficticio");
        var values = new Dictionary<string, string>
        {
            ["CurrentPassword"] = "Incorrecta-123!",
            ["NewPassword"] = "Nueva-Ficticia-983!",
            ["Confirmation"] = "Nueva-Ficticia-983!"
        };
        Assert.Equal(HttpStatusCode.OK, (await BrowserForms.PostAsync(browser, "/cuenta/clave", "/cuenta/clave", values)).StatusCode);
        values["CurrentPassword"] = SecurityApp.TestPassword;
        Assert.Equal(HttpStatusCode.Redirect, (await BrowserForms.PostAsync(browser, "/cuenta/clave", "/cuenta/clave", values)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await other.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await BrowserForms.LoginAsync(browser, "ventas.ficticio", "Nueva-Ficticia-983!")).StatusCode);
    }
}
