using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Clinical;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class ProfessionalTests
{
    [Fact]
    public async Task ProfessionalPersistsAndRejectsDuplicatesStaleEditsAndReusedSubmissions()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var scenario = await ClinicalScenario.OpenAsync(app);
        var input = new ProfessionalInput { FullName = "Profesional ficticio", Registration = "MAT-FIC" };
        var created = await scenario.Professionals.CreateAsync(input);
        Assert.True(created.Succeeded);
        Assert.True((await scenario.Professionals.CreateAsync(input)).Succeeded);
        Assert.False((await scenario.Professionals.CreateAsync(new() { FullName = " profesional   ficticio ", Registration = "mat-fic" })).Succeeded);
        var original = (await scenario.Professionals.GetAsync(created.Id!.Value))!;
        input.Version = original.Version; input.IsActive = false;
        Assert.True((await scenario.Professionals.UpdateAsync(original.Id, input)).Succeeded);
        Assert.Equal(ClinicalResultCode.Conflict, (await scenario.Professionals.UpdateAsync(original.Id, input)).Code);
        input.IsActive = true;
        Assert.Equal(ClinicalResultCode.Conflict, (await scenario.Professionals.CreateAsync(input)).Code);
        Assert.False((await scenario.Professionals.SearchAsync("MAT-FIC")).Single().IsActive);
        await using var fresh = await scenario.Factory.CreateDbContextAsync();
        Assert.Single(await fresh.Professionals.ToListAsync());
        Assert.Equal(2, await fresh.AuditEntries.CountAsync(x => x.Action == "professional.created" || x.Action == "professional.updated"));
    }

    [Theory]
    [InlineData("admin.ficticio")]
    [InlineData("ventas.ficticio")]
    public async Task NonClinicalRolesCannotReadOrWriteThroughHttpOrApplication(string user)
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var scenario = await ClinicalScenario.OpenAsync(app, user);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scenario.Professionals.SearchAsync(null));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scenario.Professionals.GetAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scenario.Professionals.CreateAsync(new() { FullName = "No permitido ficticio" }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scenario.Professionals.UpdateAsync(Guid.NewGuid(), new()));
        using var browser = app.Browser(); await BrowserForms.LoginAsync(browser, user);
        foreach (var path in new[] { "/clinica/profesionales", "/clinica/profesionales/editar" })
        {
            var denied = await browser.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
            Assert.Contains("/cuenta/denegado", denied.Headers.Location!.ToString());
        }
        Assert.Equal(HttpStatusCode.Redirect, (await BrowserForms.PostAsync(browser, "/", "/clinica/profesionales/editar", new() { ["Input.FullName"] = "No permitido ficticio" })).StatusCode);
        Assert.Empty(await scenario.Db.Professionals.ToListAsync());
    }

    [Fact]
    public async Task ProfessionalFormsRequireCsrfAndPreserveEncodedDataWithoutCreatingAnAccount()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var browser = app.Browser();
        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync("/clinica/profesionales")).StatusCode);
        await BrowserForms.LoginAsync(browser, "profesional.ficticio");
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsync("/clinica/profesionales/editar", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);
        const string path = "/clinica/profesionales/editar";
        var html = await browser.GetStringAsync(path);
        var id = Regex.Match(html, "name=\"Input.SubmissionId\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        var response = await BrowserForms.PostAsync(browser, path, path, new()
        {
            ["Input.SubmissionId"] = id, ["Input.FullName"] = "<b>Profesional ficticio</b>", ["Input.IsActive"] = "true"
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var list = await browser.GetStringAsync("/clinica/profesionales");
        Assert.Contains("&lt;b&gt;Profesional ficticio&lt;/b&gt;", list);
        using var scenario = await ClinicalScenario.OpenAsync(app);
        Assert.Equal(3, await scenario.Db.Users.CountAsync());
    }
}
