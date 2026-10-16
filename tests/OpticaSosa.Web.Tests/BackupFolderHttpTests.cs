using System.Net;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Infrastructure.Backups;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class BackupFolderHttpTests
{
    private static Dictionary<string, string> Form(Guid version) => new()
    {
        ["Version"] = version.ToString(), ["VolumeId"] = FakeBackupTargets.Volume, ["RelativeFolder"] = "Respaldos",
        ["LocalTime"] = "06:45", ["TimeZoneId"] = "UTC", ["EveryDays"] = "7", ["KeepCopies"] = "4", ["Enabled"] = "false"
    };

    [Fact]
    public async Task VisualBrowseConfirmAndSavePreservesUnsavedScheduleWithoutWritingBackups()
    {
        await using var test = new BackupTestContext(authorized: false); await test.InitializeAsync();
        using var browser = test.App.Browser(); await BrowserForms.LoginAsync(browser, "admin.ficticio");
        var form = Form((await test.Coordinator.OverviewAsync(default)).Configuration.Version);
        var browse = await BrowserForms.PostAsync(browser, "/admin/respaldos", "/admin/respaldos?handler=Browse", form);
        var html = WebUtility.HtmlDecode(await browse.Content.ReadAsStringAsync());
        Assert.Contains("Abrir Respaldos", html); Assert.Contains("value=\"06:45\"", html);
        var selected = await BrowserForms.PostAsync(browser, "/admin/respaldos", "/admin/respaldos?handler=SelectFolder&folder=Respaldos", form);
        html = WebUtility.HtmlDecode(await selected.Content.ReadAsStringAsync());
        Assert.Contains(Path.Combine(test.Targets.ServerRoot, "Respaldos"), html);
        Assert.Contains("value=\"06:45\"", html);
        form["SelectedVolumeId"] = FakeBackupTargets.Volume; form["SelectedRelativeFolder"] = "Respaldos";
        var saved = await BrowserForms.PostAsync(browser, "/admin/respaldos", "/admin/respaldos?handler=Save", form);
        if (saved.StatusCode != HttpStatusCode.Redirect)
        {
            var body = WebUtility.HtmlDecode(await saved.Content.ReadAsStringAsync());
            var errors = System.Text.RegularExpressions.Regex.Matches(body, "<li>(.*?)</li>|<p role=\"status\">(.*?)</p>");
            Assert.Fail(string.Join("; ", errors.Select(x => x.Value)));
        }
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        var state = await test.Coordinator.OverviewAsync(default);
        Assert.Equal("06:45", state.Configuration.LocalTime); Assert.Equal(7, state.Configuration.EveryDays);
        Assert.Equal(4, state.Configuration.KeepCopies); Assert.False(state.Configuration.Enabled); Assert.Null(state.NextUtc);
        Assert.False(Directory.Exists(test.Targets.Root));
    }

    [Theory]
    [InlineData("TimeZoneId", "Elegí una zona horaria válida.")]
    [InlineData("RelativeFolder", "Explorá y confirmá una carpeta de destino válida.")]
    public async Task ValidationIsSpanishAndDoesNotPersistIncompleteConfiguration(string field, string message)
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync();
        using var browser = test.App.Browser(); await BrowserForms.LoginAsync(browser, "admin.ficticio");
        var form = Form((await test.Coordinator.OverviewAsync(default)).Configuration.Version);
        form[field] = "";
        var response = await BrowserForms.PostAsync(browser, "/admin/respaldos", "/admin/respaldos?handler=Save", form);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains(message, html); Assert.DoesNotContain("field is required", html);
        Assert.Null((await test.Coordinator.OverviewAsync(default)).Configuration.VolumeId);
    }

    [Fact]
    public async Task ForgedConfirmationCannotSaveNonexistentOrEscapingDirectory()
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync();
        using var browser = test.App.Browser(); await BrowserForms.LoginAsync(browser, "admin.ficticio");
        foreach (var folder in new[] { "No-existe", "../escape" })
        {
            var form = Form((await test.Coordinator.OverviewAsync(default)).Configuration.Version);
            form["RelativeFolder"] = folder; form["SelectedRelativeFolder"] = folder; form["SelectedVolumeId"] = FakeBackupTargets.Volume;
            var response = await BrowserForms.PostAsync(browser, "/admin/respaldos", "/admin/respaldos?handler=Save", form);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Null((await test.Coordinator.OverviewAsync(default)).Configuration.VolumeId);
        }
    }

    [Fact]
    public async Task FolderEndpointsRequireCsrfAndRejectUnauthorizedCreation()
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync();
        using var browser = test.App.Browser(); await BrowserForms.LoginAsync(browser, "admin.ficticio");
        foreach (var handler in new[] { "Browse", "SelectFolder", "CreateFolder" })
            Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsync("/admin/respaldos?handler=" + handler,
                new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);
        var form = Form((await test.Coordinator.OverviewAsync(default)).Configuration.Version);
        form["NewFolderName"] = "No-autorizada"; form["ConfirmCreate"] = "true";
        var result = await BrowserForms.PostAsync(browser, "/admin/respaldos", "/admin/respaldos?handler=CreateFolder&folder=Respaldos", form);
        Assert.Contains("no está autorizado", WebUtility.HtmlDecode(await result.Content.ReadAsStringAsync()));
        Assert.False(Directory.Exists(Path.Combine(test.Targets.ServerRoot, "Respaldos", "No-autorizada")));
    }
}
