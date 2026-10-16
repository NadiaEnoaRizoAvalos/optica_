using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Application.Security;
using OpticaSosa.Infrastructure.Identity;
using OpticaSosa.Infrastructure.Persistence;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class AuthorizationTests
{
    [Theory]
    [InlineData("ventas.ficticio")]
    [InlineData("profesional.ficticio")]
    public async Task NonAdministratorsCannotManageAccountsOrReadAudit(string name)
    {
        await using var app = new SecurityApp();
        await app.InitializeAsync();
        using var browser = app.Browser();
        Assert.Equal(HttpStatusCode.Redirect, (await BrowserForms.LoginAsync(browser, name)).StatusCode);
        foreach (var path in new[] { "/admin/usuarios", "/admin/auditoria" })
        {
            var denied = await browser.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
            Assert.Contains("/cuenta/denegado", denied.Headers.Location!.ToString());
        }
        var post = await BrowserForms.PostAsync(browser, "/", "/admin/usuarios?handler=Create", new()
            { ["UserName"] = "intruso.ficticio", ["Password"] = SecurityApp.TestPassword, ["Role"] = Roles.Administrator });
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        using var scope = app.Services.CreateScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByNameAsync("intruso.ficticio"));
    }

    [Fact]
    public async Task AdministratorCanCreateAndDeactivateUsersAndExistingSessionsAreRevoked()
    {
        await using var app = new SecurityApp();
        await app.InitializeAsync();
        using var admin = app.Browser();
        await BrowserForms.LoginAsync(admin, "admin.ficticio");
        Assert.Equal(HttpStatusCode.Redirect, (await BrowserForms.PostAsync(admin, "/admin/usuarios", "/admin/usuarios?handler=Create", new()
            { ["UserName"] = "nueva.ficticia", ["Password"] = SecurityApp.TestPassword, ["Role"] = Roles.Sales })).StatusCode);
        using var employee = app.Browser();
        Assert.Equal(HttpStatusCode.Redirect, (await BrowserForms.LoginAsync(employee, "nueva.ficticia")).StatusCode);
        string id;
        using (var scope = app.Services.CreateScope())
            id = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByNameAsync("nueva.ficticia"))!.Id;
        Assert.Equal(HttpStatusCode.Redirect, (await BrowserForms.PostAsync(admin, "/admin/usuarios", "/admin/usuarios?handler=Deactivate", new() { ["userId"] = id })).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await employee.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await BrowserForms.LoginAsync(employee, "nueva.ficticia")).StatusCode);
    }

    [Fact]
    public async Task InvalidAccountCreationIsAtomicAndPrivateFilesAreNotServed()
    {
        await using var app = new SecurityApp();
        await app.InitializeAsync();
        using var browser = app.Browser();
        await BrowserForms.LoginAsync(browser, "admin.ficticio");
        foreach (var (password, role) in new[] { ("debil", Roles.Sales), (SecurityApp.TestPassword, "RolInventado") })
            Assert.Equal(HttpStatusCode.OK, (await BrowserForms.PostAsync(browser, "/admin/usuarios", "/admin/usuarios?handler=Create", new()
                { ["UserName"] = "invalido.ficticio", ["Password"] = password, ["Role"] = role })).StatusCode);
        using var scope = app.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await manager.FindByNameAsync("invalido.ficticio"));
        var paths = scope.ServiceProvider.GetRequiredService<StoragePaths>();
        await File.WriteAllTextAsync(Path.Combine(paths.AttachmentsDirectory, "probe.txt"), "synthetic private file");
        foreach (var path in new[] { "/data/optica.db", "/attachments/probe.txt", "/data/attachments/probe.txt", "/register" })
            Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync(path)).StatusCode);
        await scope.ServiceProvider.GetRequiredService<LocalProvisioning>().InitializeAsync();
        Assert.NotNull(await manager.FindByNameAsync("admin.ficticio"));
    }

    [Fact]
    public async Task ApplicationLayerRejectsUnauthorizedCallsAndProtectsTheLastAdministrator()
    {
        await using var app = new SecurityApp();
        await app.InitializeAsync();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpticaDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var service = new AccountService(db, users, new TestUser(null, false));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ListAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateAsync("no.ficticio", SecurityApp.TestPassword, Roles.Sales));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeactivateAsync("any"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReadAuditAsync());
        var administrator = (await users.FindByNameAsync("admin.ficticio"))!;
        var own = new AccountService(db, users, new TestUser(administrator.Id, true));
        Assert.False((await own.DeactivateAsync(administrator.Id)).Succeeded);
        var different = new AccountService(db, users, new TestUser("different-operator", true));
        Assert.False((await different.DeactivateAsync(administrator.Id)).Succeeded);
        Assert.True(administrator.IsActive);
    }

    [Theory]
    [InlineData(Roles.Administrator, Permissions.ReadClinical, false)]
    [InlineData(Roles.Sales, Permissions.ReadClinical, false)]
    [InlineData(Roles.Professional, Permissions.ReadClinical, true)]
    [InlineData(Roles.Professional, Permissions.ManageUsers, false)]
    [InlineData(Roles.Administrator, Permissions.ManageUsers, true)]
    public async Task ClinicalAndAdministrativePoliciesFollowMinimumPrivilege(string role, string permission, bool expected)
    {
        await using var app = new SecurityApp();
        using var scope = app.Services.CreateScope();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role) }, "test"));
        var result = await scope.ServiceProvider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(principal, null, permission);
        Assert.Equal(expected, result.Succeeded);
    }

    private sealed record TestUser(string? Id, bool Allowed) : ICurrentUser
    {
        public bool HasPermission(string permission) => Allowed;
    }
}
