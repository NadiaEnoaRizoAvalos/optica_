using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Infrastructure.Persistence;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class CustomerHttpTests
{
    [Fact]
    public async Task CustomerFormsSupportCreateEditSearchAndVisitCompletionWithRealAntiforgery()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var browser = app.Browser(); await BrowserForms.LoginAsync(browser, "ventas.ficticio");
        var created = await CustomerBrowserForms.PostAsync(browser, "/clientes/nuevo", "customer-editor", CustomerBrowserForms.NewCustomer());
        CustomerBrowserForms.Redirect(created);
        var path = created.Headers.Location!.ToString();
        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute)) path = absolute.AbsolutePath;
        Assert.StartsWith("/clientes/", path);
        Assert.Contains("FIC-HTTP-001", await browser.GetStringAsync(path));
        var edit = CustomerBrowserForms.NewCustomer(); edit["Input.Phone"] = "000-000-0101";
        CustomerBrowserForms.Redirect(await CustomerBrowserForms.PostAsync(browser, path + "/editar", "customer-editor", edit));
        Assert.Contains("000-000-0101", await browser.GetStringAsync(path));
        var search = await CustomerBrowserForms.PostAsync(browser, "/clientes", "customer-search", new() { ["Filter.Query"] = "FICHTTP001" });
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        Assert.Contains("FIC-HTTP-001", await search.Content.ReadAsStringAsync());
        CustomerBrowserForms.Redirect(await CustomerBrowserForms.PostAsync(browser, path + "/visitas/nueva", "visit-start",
            new() { ["Input.Reason"] = "Consulta comercial ficticia" }));
        Guid visitId;
        using (var scope = app.Services.CreateScope())
            visitId = await scope.ServiceProvider.GetRequiredService<OpticaDbContext>().Visits.Select(v => v.Id).SingleAsync();
        CustomerBrowserForms.Redirect(await CustomerBrowserForms.PostAsync(browser, path, "visit-close-" + visitId,
            new() { ["Closing.Completed"] = "true" }));
        Assert.Contains("Recurrente:", await browser.GetStringAsync(path));
        var duplicate = await CustomerBrowserForms.PostAsync(browser, "/clientes/nuevo", "customer-editor", CustomerBrowserForms.NewCustomer("fic.http.001"));
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Contains("Ya existe un cliente", await duplicate.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AnonymousAndForgedPostsAreRejectedAndOnlyAdminCanDeactivateFromHttp()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var anonymous = app.Browser();
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync("/clientes")).StatusCode);
        using var sales = app.Browser(); await BrowserForms.LoginAsync(sales, "ventas.ficticio");
        Assert.Equal(HttpStatusCode.BadRequest, (await sales.PostAsync("/clientes/nuevo", new FormUrlEncodedContent(CustomerBrowserForms.NewCustomer()))).StatusCode);
        var created = await CustomerBrowserForms.PostAsync(sales, "/clientes/nuevo", "customer-editor", CustomerBrowserForms.NewCustomer());
        CustomerBrowserForms.Redirect(created);
        var path = created.Headers.Location!.ToString();
        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute)) path = absolute.AbsolutePath;
        var denied = await sales.GetAsync(path + "/inactivar");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Contains("/cuenta/denegado", denied.Headers.Location!.ToString());
        using var admin = app.Browser(); await BrowserForms.LoginAsync(admin, "admin.ficticio");
        CustomerBrowserForms.Redirect(await CustomerBrowserForms.PostAsync(admin, path + "/inactivar", "customer-deactivate",
            new() { ["Input.Confirmed"] = "true" }));
        Assert.Contains("Inactivo", await admin.GetStringAsync(path));
    }

    [Fact]
    public async Task InvalidCustomerCannotBeCreatedEvenByPostingDirectly()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var browser = app.Browser(); await BrowserForms.LoginAsync(browser, "admin.ficticio");
        var response = await CustomerBrowserForms.PostAsync(browser, "/clientes/nuevo", "customer-editor", CustomerBrowserForms.NewCustomer(""));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = app.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<OpticaDbContext>().Customers.ToListAsync());
    }

    [Fact]
    public async Task PossibleDuplicatesNeedExplicitConfirmationAndDoublePostingDoesNotCreateAnotherClient()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var browser = app.Browser(); await BrowserForms.LoginAsync(browser, "admin.ficticio");
        CustomerBrowserForms.Redirect(await CustomerBrowserForms.PostAsync(browser, "/clientes/nuevo", "customer-editor", CustomerBrowserForms.NewCustomer()));
        var input = CustomerBrowserForms.NewCustomer("FIC-HTTP-002");
        var warning = await CustomerBrowserForms.PostAsync(browser, "/clientes/nuevo", "customer-editor", input);
        Assert.Equal(HttpStatusCode.OK, warning.StatusCode);
        var html = await warning.Content.ReadAsStringAsync();
        Assert.Contains("Hay coincidencias", html);
        var fields = CustomerBrowserForms.Read(html, "customer-editor");
        foreach (var item in input) fields[item.Key] = item.Value;
        fields["Input.ConfirmPossibleDuplicate"] = "true";
        CustomerBrowserForms.Redirect(await browser.PostAsync("/clientes/nuevo", new FormUrlEncodedContent(fields)));
        CustomerBrowserForms.Redirect(await browser.PostAsync("/clientes/nuevo", new FormUrlEncodedContent(fields)));
        using var scope = app.Services.CreateScope();
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<OpticaDbContext>().Customers.CountAsync());
    }
}
