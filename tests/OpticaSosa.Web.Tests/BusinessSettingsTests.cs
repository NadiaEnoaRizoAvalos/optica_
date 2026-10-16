using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OpticaSosa.Application.Business;
using OpticaSosa.Application.Security;
using OpticaSosa.Infrastructure.Business;
using OpticaSosa.Infrastructure.Persistence;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class BusinessSettingsTests
{
    [Fact]
    public async Task SettingsAndLogoSurviveServerRestartAndRemainIndependentFromCustomers()
    {
        var first = new SecurityApp(preserveData: true);
        string root = first.Root;
        Guid? logoVersion;
        try
        {
            await first.InitializeAsync();
            using var admin = first.Browser(); await BrowserForms.LoginAsync(admin, "admin.ficticio");
            BusinessForms.Saved(await BusinessForms.SaveAsync(admin, new()
            {
                ["Input.TradeName"] = "Negocio ficticio para pruebas", ["Input.Address"] = "Dirección privada ficticia",
                ["Input.Province"] = "Provincia ficticia", ["Input.Locality"] = "Localidad ficticia",
                ["Input.Phone"] = "000-000-0101", ["Input.WhatsApp"] = "000-000-0102",
                ["Input.Email"] = "negocio@ejemplo.invalid", ["Input.Cuit"] = "00-00000000-0",
                ["Input.AdditionalDetails"] = "Dato adicional ficticio"
            }, BusinessForms.Logo()));
            using var scope = first.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OpticaDbContext>();
            var row = await db.BusinessSettings.AsNoTracking().SingleAsync();
            logoVersion = row.LogoVersion;
            Assert.Equal("00000000000", row.Profile.Cuit);
            Assert.Empty(await db.Customers.ToListAsync());
            var audit = await db.AuditEntries.SingleAsync(x => x.Action == "business.updated");
            Assert.Equal("business", audit.TargetId);
        }
        finally { await first.DisposeAsync(); }

        await using var restarted = new SecurityApp(root);
        using var browser = restarted.Browser();
        var login = await browser.GetStringAsync("/cuenta/ingresar");
        Assert.Contains("Negocio ficticio para pruebas", login);
        Assert.DoesNotContain("Dirección privada ficticia", login);
        Assert.DoesNotContain("negocio@ejemplo.invalid", login);
        var logo = await browser.GetAsync("/negocio/logo/" + logoVersion);
        Assert.Equal(HttpStatusCode.OK, logo.StatusCode);
        Assert.Equal("image/png", logo.Content.Headers.ContentType!.MediaType);
        await BrowserForms.LoginAsync(browser, "ventas.ficticio");
        Assert.Contains("Negocio ficticio para pruebas", await browser.GetStringAsync("/clientes"));
    }

    [Theory]
    [InlineData("ventas.ficticio")]
    [InlineData("profesional.ficticio")]
    public async Task StaffCannotReadPrivateConfigurationOrModifyIt(string user)
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var browser = app.Browser(); await BrowserForms.LoginAsync(browser, user);
        var response = await browser.GetAsync("/admin/negocio");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/cuenta/denegado", response.Headers.Location!.ToString());
        var denied = await BrowserForms.PostAsync(browser, "/", "/admin/negocio", new() { ["Input.TradeName"] = "No permitido" });
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        using var scope = app.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<OpticaDbContext>().BusinessSettings.ToListAsync());
    }

    [Fact]
    public async Task ApplicationRequiresAdminAndHttpRequiresAntiforgery()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var scope = app.Services.CreateScope();
        var service = new BusinessSettingsService(scope.ServiceProvider.GetRequiredService<IDbContextFactory<OpticaDbContext>>(),
            new Forbidden(), NullLogger<BusinessSettingsService>.Instance);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetForAdministrationAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SaveAsync(new(null, null, null, null, null, null, null, null, null), Guid.Empty, null, false));
        using var admin = app.Browser(); await BrowserForms.LoginAsync(admin, "admin.ficticio");
        var forged = await admin.PostAsync("/admin/negocio", new FormUrlEncodedContent(new Dictionary<string, string> { ["Input.TradeName"] = "No permitido" }));
        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
    }

    [Fact]
    public async Task BlankNameAndRemovedLogoRestoreGenericBrandAndInvalidUpdatesAreAtomic()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var admin = app.Browser(); await BrowserForms.LoginAsync(admin, "admin.ficticio");
        BusinessForms.Saved(await BusinessForms.SaveAsync(admin, new() { ["Input.TradeName"] = "Marca ficticia" }, BusinessForms.Logo()));
        var stale = await BusinessForms.ReadAsync(admin);
        BusinessForms.Saved(await BusinessForms.SaveAsync(admin, new() { ["Input.TradeName"] = "Marca actual ficticia" }));
        Assert.Equal(HttpStatusCode.OK, (await BusinessForms.SaveAsync(admin, new() { ["Input.TradeName"] = "No debe persistir" }, staleForm: stale)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await BusinessForms.SaveAsync(admin, new() { ["Input.TradeName"] = "Tampoco debe persistir" },
            System.Text.Encoding.UTF8.GetBytes("<svg>invalid</svg>"), "logo.png", "image/png")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await BusinessForms.SaveAsync(admin, new() { ["Input.Cuit"] = "invalido" })).StatusCode);
        using (var scope = app.Services.CreateScope())
            Assert.Equal("Marca actual ficticia", (await scope.ServiceProvider.GetRequiredService<OpticaDbContext>().BusinessSettings.SingleAsync()).Profile.TradeName);
        BusinessForms.Saved(await BusinessForms.SaveAsync(admin, new() { ["Input.TradeName"] = " ", ["Input.RemoveLogo"] = "true" }));
        using var finalScope = app.Services.CreateScope();
        var row = await finalScope.ServiceProvider.GetRequiredService<OpticaDbContext>().BusinessSettings.SingleAsync();
        Assert.Null(row.Profile.TradeName); Assert.Null(row.LogoPng); Assert.Null(row.LogoVersion);
        var html = WebUtility.HtmlDecode(await admin.GetStringAsync("/"));
        Assert.Contains(BusinessBranding.DefaultName, html);
        Assert.DoesNotContain("Óptica Sosa", html);
    }

    private sealed class Forbidden : ICurrentUser
    {
        public string? Id => "synthetic-unauthorized";
        public bool HasPermission(string permission) => false;
    }
}
