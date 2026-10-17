using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Domain.Work;
using OpticaSosa.Infrastructure.Persistence;
using OpticaSosa.Infrastructure.Viewer;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class ViewerHttpTests
{
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public async Task AllRoleCombinationsEnforceClinicalDocumentsQualityAndAdministrativePermissions(int mask)
    {
        await using var fixture = new ViewerFixture(); await fixture.InitializeAsync();
        var original = await ViewerFixture.HashesAsync(fixture.Backup);
        using var browser = fixture.Viewer.Browser();
        Assert.Equal(HttpStatusCode.Redirect, (await BrowserForms.LoginAsync(browser, $"mixta{mask}.ficticia")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync($"/clientes/{fixture.Customer}")).StatusCode);
        var clinical = (mask & 4) != 0; var administrative = (mask & 1) != 0; var operational = (mask & 6) != 0;
        var record = $"/clientes/{fixture.Customer}/recetas/{fixture.Record}";
        foreach (var (path, allowed) in new[] { (record, clinical),
                     (record + $"/documentos/{fixture.Image}", clinical), ("/trabajos", operational), ("/accesos", administrative) })
        {
            var response = await browser.GetAsync(path);
            Assert.Equal(allowed ? HttpStatusCode.OK : HttpStatusCode.Redirect, response.StatusCode);
            if (!allowed) Assert.Contains("/cuenta/denegado", response.Headers.Location!.ToString());
        }
        if (operational)
        {
            var html = await browser.GetStringAsync($"/trabajos/{fixture.Work}");
            Assert.Contains("Delivery", html);
            Assert.Equal(clinical, html.Contains("Technical", StringComparison.Ordinal));
        }
        Assert.Equal(original, await ViewerFixture.HashesAsync(fixture.Backup));
    }

    [Fact]
    public async Task ClinicalHistoryVersionsPrecisionOriginalBytesAndQualityRemainReadOnly()
    {
        await using var fixture = new ViewerFixture(); await fixture.InitializeAsync();
        var source = await ViewerFixture.HashesAsync(fixture.Backup);
        using var browser = fixture.Viewer.Browser(); await BrowserForms.LoginAsync(browser, "profesional.ficticio");
        var snapshot = fixture.Viewer.Services.GetRequiredService<ViewerSnapshot>();
        var before = await ViewerFixture.HashesAsync(snapshot.Directory);
        var path = $"/clientes/{fixture.Customer}/recetas/{fixture.Record}";
        var latest = await browser.GetStringAsync(path); Assert.Matches("1[,.]375", latest); Assert.Matches("62[,.]25", latest);
        var previous = await browser.GetStringAsync(path + "?revision=1"); Assert.Matches("1[,.]125", previous);
        Assert.Contains("Matr", previous); Assert.Contains("Versi", previous);
        foreach (var (id, bytes, mime) in new[] { (fixture.Image, BusinessForms.Logo(), "image/png"), (fixture.Pdf, AttachmentTestContext.Pdf(), "application/pdf") })
        {
            var document = await browser.GetAsync(path + $"/documentos/{id}");
            Assert.Equal(bytes, await document.Content.ReadAsByteArrayAsync());
            Assert.Equal(mime, document.Content.Headers.ContentType!.MediaType);
            Assert.Contains("no-store", document.Headers.CacheControl!.ToString());
            Assert.Equal("nosniff", document.Headers.GetValues("X-Content-Type-Options").Single());
        }
        var wrongCustomer = await browser.GetAsync($"/clientes/{Guid.NewGuid()}/recetas/{fixture.Record}/documentos/{fixture.Image}");
        Assert.Equal(HttpStatusCode.NotFound, wrongCustomer.StatusCode);
        foreach (var target in new[] { "/", path, path + $"/documentos/{fixture.Image}", "/admin/usuarios", "/trabajos" })
            Assert.Equal(HttpStatusCode.MethodNotAllowed, (await browser.PostAsync(target, new StringContent("intento ficticio"))).StatusCode);
        Assert.Equal(before, await ViewerFixture.HashesAsync(snapshot.Directory));
        Assert.Equal(source, await ViewerFixture.HashesAsync(fixture.Backup));
        Assert.Contains("clinical.revision.read", await File.ReadAllTextAsync(Path.Combine(snapshot.Workspace, "viewer-audit.jsonl")));
    }

    [Fact]
    public async Task LocalRevocationInvalidatesExistingSessionsAndLoginWithoutChangingSnapshotRoles()
    {
        await using var fixture = new ViewerFixture(); await fixture.InitializeAsync();
        using var professional = fixture.Viewer.Browser(); await BrowserForms.LoginAsync(professional, "profesional.ficticio");
        using var admin = fixture.Viewer.Browser(); await BrowserForms.LoginAsync(admin, "admin.ficticio");
        using var scope = fixture.Viewer.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpticaDbContext>();
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.UserName == "profesional.ficticio");
        var snapshot = fixture.Viewer.Services.GetRequiredService<ViewerSnapshot>(); var before = await ViewerFixture.HashesAsync(snapshot.Directory);
        var values = new Dictionary<string, string> { ["userId"] = user.Id, ["confirmed"] = "true" };
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/accesos", new FormUrlEncodedContent(values))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await BrowserForms.PostAsync(admin, "/accesos", "/accesos", values)).StatusCode);
        Assert.Contains("/cuenta/ingresar", (await professional.GetAsync($"/clientes/{fixture.Customer}/recetas/{fixture.Record}")).Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.OK, (await BrowserForms.LoginAsync(professional, "profesional.ficticio")).StatusCode);
        Assert.Equal(before, await ViewerFixture.HashesAsync(snapshot.Directory));
        Assert.Contains(user.Id, await File.ReadAllTextAsync(Path.Combine(snapshot.Workspace, "revocations.jsonl")));
    }

    [Fact]
    public async Task DatabaseGuardAndSqliteBothRejectWritesAndViewerDoesNotExposePrivateFiles()
    {
        await using var fixture = new ViewerFixture(); await fixture.InitializeAsync();
        using var browser = fixture.Viewer.Browser(); await BrowserForms.LoginAsync(browser, "admin.ficticio");
        using var scope = fixture.Viewer.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpticaDbContext>();
        var user = await db.Users.FirstAsync(); user.IsActive = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => db.Database.ExecuteSqlRawAsync("UPDATE AspNetUsers SET IsActive=0"));
        foreach (var path in new[] { "/snapshot/optica.db", "/viewer-audit.jsonl", "/revocations.jsonl", "/attachments/clinical/" + fixture.Image.ToString("N") + ".bin", "/admin/usuarios" })
            Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync(path)).StatusCode);
        var forged = new HttpRequestMessage(HttpMethod.Get, "/") { Headers = { Host = "sitio-ajeno.invalid" } };
        Assert.Equal(HttpStatusCode.Forbidden, (await browser.SendAsync(forged)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.GetAsync($"/clientes/{fixture.Customer}?clinicalPage=-1")).StatusCode);
    }
}
