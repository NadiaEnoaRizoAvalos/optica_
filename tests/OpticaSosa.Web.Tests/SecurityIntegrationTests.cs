using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Security;
using OpticaSosa.Infrastructure.Identity;
using OpticaSosa.Infrastructure.Persistence;
using OpticaSosa.Infrastructure.Viewer;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class SecurityIntegrationTests
{
    [Fact]
    public async Task ViewerAttachmentStoreCannotWriteEvenWhenCalledOutsideItsEndpoints()
    {
        await using var fixture = new ViewerFixture(); await fixture.InitializeAsync();
        using var browser = fixture.Viewer.Browser();
        var snapshot = fixture.Viewer.Services.GetRequiredService<ViewerSnapshot>();
        var before = await ViewerFixture.HashesAsync(snapshot.Directory);
        using var scope = fixture.Viewer.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IPrivateAttachmentStore>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.WriteAsync(Guid.NewGuid(), BusinessForms.Logo()));
        Assert.Equal(before, await ViewerFixture.HashesAsync(snapshot.Directory));
    }

    [Theory]
    [InlineData("text/html")]
    [InlineData("image/svg+xml")]
    public async Task ATrustedLegacyBackupCannotServeUnsupportedInlineDocumentTypes(string mime)
    {
        await using var fixture = new ViewerFixture(); await fixture.InitializeAsync();
        using (var scope = fixture.Source.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<OpticaDbContext>().Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE ClinicalAttachments SET ContentType={mime} WHERE Id={fixture.Image}");
        await fixture.RefreshAsync();
        using var browser = fixture.Viewer.Browser(); await BrowserForms.LoginAsync(browser, "profesional.ficticio");
        var result = await browser.GetAsync($"/clientes/{fixture.Customer}/recetas/{fixture.Record}/documentos/{fixture.Image}");
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, result.StatusCode);
    }

    [Fact]
    public async Task SimultaneousLocalRevocationsCannotRemoveBothAdministrators()
    {
        await using var fixture = new ViewerFixture(); await fixture.InitializeAsync();
        using var browser = fixture.Viewer.Browser();
        using var first = fixture.Viewer.Services.CreateScope(); using var second = fixture.Viewer.Services.CreateScope();
        var a = await AccountsAsync(first, "admin.ficticio"); var b = await AccountsAsync(second, "mixta5.ficticia");
        async Task<bool> RevokeAsync(ViewerAccounts service, string target)
        {
            try { return (await service.RevokeAsync(target, true)).Succeeded; }
            catch (UnauthorizedAccessException) { return false; }
        }
        var results = await Task.WhenAll(Task.Run(() => RevokeAsync(a.Service, b.Id)), Task.Run(() => RevokeAsync(b.Service, a.Id)));
        Assert.Single(results, succeeded => succeeded);
        var access = fixture.Viewer.Services.GetRequiredService<ViewerAccess>();
        Assert.NotEqual(access.IsRevoked(a.Id), access.IsRevoked(b.Id));
    }

    [Theory]
    [InlineData("profesional.ficticio")]
    [InlineData("ventas.ficticio")]
    public async Task CapturedDatabaseRolesRejectStaleAdministrativeClaimsAtTheApplicationBoundary(string name)
    {
        await using var fixture = new ViewerFixture(); await fixture.InitializeAsync();
        using var browser = fixture.Viewer.Browser(); using var scope = fixture.Viewer.Services.CreateScope();
        var caller = await AccountsAsync(scope, name);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => caller.Service.ListAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => caller.Service.RevokeAsync("any", true));
    }

    [Fact]
    public async Task IncompatibleSchemaIsRejectedWithoutMigratingOrChangingTheBackup()
    {
        await using var fixture = new ViewerFixture(); await fixture.InitializeAsync();
        using (var scope = fixture.Source.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<OpticaDbContext>().Database.ExecuteSqlRawAsync(
                "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('99999999999999_FuturoFicticio', '10.0.12')");
        await fixture.RefreshAsync(); var before = await ViewerFixture.HashesAsync(fixture.Backup);
        var error = Assert.ThrowsAny<Exception>(() => fixture.Viewer.Browser());
        Assert.Contains("base incompatible", error.ToString());
        Assert.Equal(before, await ViewerFixture.HashesAsync(fixture.Backup));
    }

    [Fact]
    public async Task InstancesHaveIndependentAuthenticationAntiforgeryAndTempDataCookiesAndSupportUnicodePaths()
    {
        await using var fixture = new ViewerFixture(); await fixture.InitializeAsync();
        using var first = fixture.Viewer.Browser(); var login = await BrowserForms.LoginAsync(first, "admin.ficticio");
        var authCookie = login.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("OpticaViewer.", StringComparison.Ordinal)).Split(';')[0];
        await using var other = new ViewerApp(fixture.Backup, Path.Combine(fixture.Source.Root, "visor con espacios á"), fixture.Fingerprint);
        using var second = other.Browser();
        var replay = new HttpRequestMessage(HttpMethod.Get, "/"); replay.Headers.Add("Cookie", authCookie);
        Assert.Contains("/cuenta/ingresar", (await second.SendAsync(replay)).Headers.Location!.ToString());
        Assert.NotEqual(fixture.Viewer.Services.GetRequiredService<IOptions<AntiforgeryOptions>>().Value.Cookie.Name,
            other.Services.GetRequiredService<IOptions<AntiforgeryOptions>>().Value.Cookie.Name);
        Assert.NotEqual(fixture.Viewer.Services.GetRequiredService<IOptions<CookieTempDataProviderOptions>>().Value.Cookie.Name,
            other.Services.GetRequiredService<IOptions<CookieTempDataProviderOptions>>().Value.Cookie.Name);
        Assert.Equal(HttpStatusCode.Redirect, (await BrowserForms.LoginAsync(second, "profesional.ficticio")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await second.GetAsync($"/clientes/{fixture.Customer}")).StatusCode);
    }

    [Fact]
    public async Task ThePrimaryApplicationDoesNotAcquireViewerRoutesOrDefaultDemonstrationAccounts()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync(); using var browser = app.Browser();
        await BrowserForms.LoginAsync(browser, "admin.ficticio");
        foreach (var path in new[] { "/accesos", "/trabajos", "/viewer.css" })
            Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/admin/usuarios")).StatusCode);
        using var scope = app.Services.CreateScope();
        Assert.Equal(3, await scope.ServiceProvider.GetRequiredService<OpticaDbContext>().Users.CountAsync());
    }

    [Fact]
    public async Task ExtensiveClinicalHistoryIsPagedWithoutTruncationInTheViewer()
    {
        await using var fixture = new ViewerFixture(); await fixture.InitializeAsync();
        using (var pro = await ClinicalScenario.OpenAsync(fixture.Source))
        {
            var professional = await pro.Db.Professionals.Select(x => x.Id).SingleAsync();
            for (var i = 0; i < 27; i++) Assert.True((await pro.Clinical.CreateAsync(fixture.Customer, ClinicalScenario.Input(professional))).Succeeded);
        }
        await fixture.RefreshAsync();
        using var browser = fixture.Viewer.Browser(); await BrowserForms.LoginAsync(browser, "profesional.ficticio");
        var page1 = await browser.GetStringAsync($"/clientes/{fixture.Customer}");
        var page2 = await browser.GetStringAsync($"/clientes/{fixture.Customer}?clinicalPage=2");
        var pattern = "href=\"/clientes/" + fixture.Customer + "/recetas/";
        Assert.Equal(25, Regex.Matches(page1, pattern).Count); Assert.Equal(4, Regex.Matches(page2, pattern).Count);
        Assert.Contains("clinicalPage=2", page1);
    }

    private static async Task<(ViewerAccounts Service, string Id)> AccountsAsync(IServiceScope scope, string name)
    {
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByNameAsync(name))!;
        var service = new ViewerAccounts(scope.ServiceProvider.GetRequiredService<OpticaDbContext>(), users,
            new StaleActor(user.Id), scope.ServiceProvider.GetRequiredService<ViewerAccess>(), scope.ServiceProvider.GetRequiredService<ViewerJournal>());
        return (service, user.Id);
    }
    private sealed record StaleActor(string Id) : ICurrentUser
    {
        public bool HasPermission(string permission) => true;
    }
}
