using System.Net;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Infrastructure.Viewer;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class ViewerSafetyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NeitherSourceNorWorkspaceCanBePlacedInsideTheWebRoot(bool sourcePublic)
    {
        await using var fixture = new ViewerFixture(); await fixture.InitializeAsync();
        var before = await ViewerFixture.HashesAsync(fixture.Backup);
        var publicRoot = sourcePublic ? Path.GetDirectoryName(fixture.Backup)! : Path.Combine(fixture.Source.Root, "public-ficticio");
        Directory.CreateDirectory(publicRoot);
        var workspace = Path.Combine(fixture.Source.Root, "private-ficticio");
        if (!sourcePublic) workspace = Path.Combine(publicRoot, "workspace");
        await using var app = new ViewerApp(fixture.Backup, workspace, fixture.Fingerprint, publicRoot: publicRoot);
        var failure = Assert.ThrowsAny<Exception>(() => app.Browser());
        Assert.Contains("carpeta pública", failure.ToString());
        Assert.Equal(before, await ViewerFixture.HashesAsync(fixture.Backup));
    }

    [Theory]
    [InlineData("untrusted")]
    [InlineData("corrupt")]
    [InlineData("existing")]
    [InlineData("unconfirmed")]
    [InlineData("nested")]
    public async Task UnsafePreparationFailsClosedAndNeverStartsTheViewer(string kind)
    {
        await using var fixture = new ViewerFixture(); await fixture.InitializeAsync();
        var before = await ViewerFixture.HashesAsync(fixture.Backup);
        var trust = kind == "untrusted" ? new string('0', 64) : fixture.Fingerprint;
        var workspace = fixture.Workspace;
        var source = fixture.Backup;
        if (kind == "nested") { workspace = Path.Combine(source, "nueva"); source += Path.DirectorySeparatorChar; }
        if (kind == "existing") Directory.CreateDirectory(workspace);
        if (kind == "corrupt")
        {
            // Sólo este respaldo ficticio de este caso; no es el kit congelado.
            await File.AppendAllTextAsync(Path.Combine(fixture.Backup, "configuration.json"), "ALTERADO FICTICIO");
            before = await ViewerFixture.HashesAsync(fixture.Backup);
        }
        await using var rejected = new ViewerApp(source, workspace, trust, kind != "unconfirmed");
        var error = Assert.ThrowsAny<Exception>(() => rejected.Browser());
        var expected = kind switch
        {
            "untrusted" => "firmante no está autorizado", "corrupt" => "hash", "existing" => "carpeta de trabajo nueva",
            "nested" => "separado del respaldo", _ => "datos ficticios"
        };
        Assert.Contains(expected, error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await ViewerFixture.HashesAsync(fixture.Backup));
    }

    [Fact]
    public async Task AnonymousRequestsDoNotExposeClientsDocumentsOrQualityAndForgeryCannotLogIn()
    {
        await using var fixture = new ViewerFixture(); await fixture.InitializeAsync();
        using var browser = fixture.Viewer.Browser();
        foreach (var path in new[] { "/", $"/clientes/{fixture.Customer}", "/trabajos", $"/trabajos/{fixture.Work}",
                     $"/clientes/{fixture.Customer}/recetas/{fixture.Record}/documentos/{fixture.Image}", "/accesos" })
        {
            var response = await browser.GetAsync(path); Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/cuenta/ingresar", response.Headers.Location!.ToString());
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsync("/cuenta/ingresar", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["UserName"] = "admin.ficticio", ["Password"] = SecurityApp.TestPassword }))).StatusCode);
    }

    [Fact]
    public async Task FailedPasswordsLockOnlyTheLocalViewerWithoutChangingCredentialsOrAuditInTheCopy()
    {
        await using var fixture = new ViewerFixture(); await fixture.InitializeAsync();
        using var browser = fixture.Viewer.Browser();
        var snapshot = fixture.Viewer.Services.GetRequiredService<ViewerSnapshot>(); var before = await ViewerFixture.HashesAsync(snapshot.Directory);
        for (var attempt = 0; attempt < 5; attempt++)
            Assert.Equal(HttpStatusCode.OK, (await BrowserForms.LoginAsync(browser, "profesional.ficticio", "Incorrecta-Ficticia-000!")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await BrowserForms.LoginAsync(browser, "profesional.ficticio")).StatusCode);
        Assert.Equal(before, await ViewerFixture.HashesAsync(snapshot.Directory));
        var audit = await File.ReadAllTextAsync(Path.Combine(snapshot.Workspace, "viewer-audit.jsonl"));
        Assert.DoesNotContain("Incorrecta", audit); Assert.DoesNotContain(SecurityApp.TestPassword, audit);
        Assert.DoesNotContain("VISOR-FICTICIO", audit); Assert.Contains("viewer.login.failed", audit);
    }
}
