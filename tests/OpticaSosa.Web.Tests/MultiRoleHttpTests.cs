using System.Net;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Security;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class MultiRoleHttpTests
{
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public async Task BrowserCreatesEveryCombinationAndEndpointsGrantOnlyItsExplicitPermissions(int mask)
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var admin = app.Browser(); await BrowserForms.LoginAsync(admin, "admin.ficticio");
        var roles = Roles.All.Where((_, i) => (mask & (1 << i)) != 0).ToArray();
        var values = new Dictionary<string, string> { ["UserName"] = "mixta.ficticia", ["Password"] = SecurityApp.TestPassword };
        for (var i = 0; i < roles.Length; i++) values[$"SelectedRoles[{i}]"] = roles[i];
        Assert.Equal(HttpStatusCode.Redirect, (await BrowserForms.PostAsync(admin, "/admin/usuarios", "/admin/usuarios?handler=Create", values)).StatusCode);
        var html = WebUtility.HtmlDecode(await admin.GetStringAsync("/admin/usuarios"));
        Assert.Contains("Editar roles de mixta.ficticia", html);
        Assert.Contains("type=\"checkbox\" name=\"SelectedRoles\"", html);
        Assert.Contains(string.Join(" · ", roles.Order().Select(Roles.Label)), html);

        using var browser = app.Browser(); await BrowserForms.LoginAsync(browser, "mixta.ficticia");
        foreach (var (path, permission) in new[] { ("/admin/usuarios", Permissions.ManageUsers),
                     ("/clinica/registros", Permissions.ReadClinical), ("/pedidos", Permissions.ReadOrders) })
        {
            var response = await browser.GetAsync(path);
            var allowed = roles.Any(x => Permissions.Grants(x, permission));
            Assert.Equal(allowed ? HttpStatusCode.OK : HttpStatusCode.Redirect, response.StatusCode);
            if (!allowed) Assert.Contains("/cuenta/denegado", response.Headers.Location!.ToString());
        }
        // También el endpoint de descarga debe exigir Profesional, aun con un ID inventado.
        var download = await browser.GetAsync($"/clientes/{Guid.NewGuid()}/clinica/{Guid.NewGuid()}/adjuntos/{Guid.NewGuid()}/descargar");
        Assert.Equal(roles.Contains(Roles.Professional) ? HttpStatusCode.NotFound : HttpStatusCode.Redirect, download.StatusCode);
    }

    [Fact]
    public async Task RemovingProfessionalRevokesEveryOpenSessionAndNewLoginCannotReadOrPostClinicalData()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var scenario = await AccountScenario.OpenAsync(app);
        Assert.True((await scenario.SetRolesAsync("ventas.ficticio", Roles.Professional, Roles.Sales)).Succeeded);
        using var first = app.Browser(); using var second = app.Browser();
        await BrowserForms.LoginAsync(first, "ventas.ficticio"); await BrowserForms.LoginAsync(second, "ventas.ficticio");
        Assert.Equal(HttpStatusCode.OK, (await first.GetAsync("/clinica/registros")).StatusCode);
        using var admin = app.Browser(); await BrowserForms.LoginAsync(admin, "admin.ficticio");
        var previous = await scenario.SummaryAsync("ventas.ficticio");
        Assert.Equal(HttpStatusCode.Redirect, (await ChangeAsync(admin, previous, [Roles.Sales])).StatusCode);
        foreach (var browser in new[] { first, second })
        {
            var revoked = await browser.GetAsync("/clinica/registros");
            Assert.Equal(HttpStatusCode.Redirect, revoked.StatusCode);
            Assert.Contains("/cuenta/ingresar", revoked.Headers.Location!.ToString());
        }
        Assert.Equal(HttpStatusCode.Redirect, (await BrowserForms.LoginAsync(first, "ventas.ficticio")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await first.GetAsync("/pedidos")).StatusCode);
        var denied = await first.GetAsync("/clinica/registros");
        Assert.Contains("/cuenta/denegado", denied.Headers.Location!.ToString());
        var post = await BrowserForms.PostAsync(first, "/", $"/clientes/{Guid.NewGuid()}/clinica/nuevo", new());
        Assert.Contains("/cuenta/denegado", post.Headers.Location!.ToString());
        Assert.Empty(await scenario.Db.ClinicalRecords.ToListAsync());
    }

    [Fact]
    public async Task RemovingAdministratorRevokesManagementEndpointsButKeepsExplicitClinicalRole()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var scenario = await AccountScenario.OpenAsync(app);
        Assert.True((await scenario.SetRolesAsync("profesional.ficticio", Roles.Administrator, Roles.Professional)).Succeeded);
        using var mixed = app.Browser(); await BrowserForms.LoginAsync(mixed, "profesional.ficticio");
        Assert.Equal(HttpStatusCode.OK, (await mixed.GetAsync("/admin/usuarios")).StatusCode);
        using var admin = app.Browser(); await BrowserForms.LoginAsync(admin, "admin.ficticio");
        Assert.Equal(HttpStatusCode.Redirect, (await ChangeAsync(admin, await scenario.SummaryAsync("profesional.ficticio"), [Roles.Professional])).StatusCode);
        Assert.Contains("/cuenta/ingresar", (await mixed.GetAsync("/admin/usuarios")).Headers.Location!.ToString());
        await BrowserForms.LoginAsync(mixed, "profesional.ficticio");
        Assert.Equal(HttpStatusCode.OK, (await mixed.GetAsync("/clinica/registros")).StatusCode);
        var denied = await BrowserForms.PostAsync(mixed, "/", "/admin/usuarios?handler=Roles", new()
        {
            ["userId"] = (await scenario.SummaryAsync("profesional.ficticio")).Id,
            ["SelectedRoles[0]"] = Roles.Administrator, ["confirmed"] = "true"
        });
        Assert.Contains("/cuenta/denegado", denied.Headers.Location!.ToString());
        Assert.Equal(new[] { Roles.Professional }, (await scenario.SummaryAsync("profesional.ficticio")).Roles);
    }

    [Fact]
    public async Task RoleEditingRequiresCsrfConfirmationAndNonEmptySelection()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var scenario = await AccountScenario.OpenAsync(app);
        using var admin = app.Browser(); await BrowserForms.LoginAsync(admin, "admin.ficticio");
        var previous = await scenario.SummaryAsync("ventas.ficticio");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/admin/usuarios?handler=Roles", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["userId"] = previous.Id, ["concurrencyStamp"] = previous.ConcurrencyStamp,
            ["SelectedRoles"] = Roles.Professional, ["confirmed"] = "true"
        }))).StatusCode);
        await ChangeAsync(admin, previous, [Roles.Professional], false);
        await ChangeAsync(admin, previous, []);
        // Una solicitud incompleta no debe causar un error del servidor ni modificar cuentas.
        Assert.Equal(HttpStatusCode.Redirect, (await BrowserForms.PostAsync(admin, "/admin/usuarios", "/admin/usuarios?handler=Roles", new()
        {
            ["SelectedRoles[0]"] = Roles.Professional, ["confirmed"] = "true"
        })).StatusCode);
        var after = await scenario.SummaryAsync("ventas.ficticio");
        Assert.Equal(previous.Roles, after.Roles); Assert.Equal(previous.ConcurrencyStamp, after.ConcurrencyStamp);
        Assert.False(await scenario.Db.AuditEntries.AnyAsync(x => x.Action == "user.roles.changed"));
    }

    private static Task<HttpResponseMessage> ChangeAsync(HttpClient browser, AccountSummary account, string[] roles, bool confirmed = true)
    {
        var values = new Dictionary<string, string>
        {
            ["userId"] = account.Id, ["concurrencyStamp"] = account.ConcurrencyStamp,
            ["confirmed"] = confirmed.ToString()
        };
        for (var i = 0; i < roles.Length; i++) values[$"SelectedRoles[{i}]"] = roles[i];
        return BrowserForms.PostAsync(browser, "/admin/usuarios", "/admin/usuarios?handler=Roles", values);
    }
}
