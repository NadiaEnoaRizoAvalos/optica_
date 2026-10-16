using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Clinical;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class ClinicalHttpTests
{
    [Fact]
    public async Task BrowserCanCreateInspectAndCorrectAPrescriptionWithExactPrecision()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var customers = await CustomerScenario.OpenAsync(app);
        var customer = (await customers.Customers.CreateAsync(CustomerScenario.Input())).CustomerId!.Value;
        using var scenario = await ClinicalScenario.OpenAsync(app);
        var professional = await scenario.AddProfessionalAsync();
        using var browser = app.Browser(); await BrowserForms.LoginAsync(browser, "profesional.ficticio");
        var path = $"/clientes/{customer}/clinica/nuevo";
        var html = await browser.GetStringAsync(path);
        Assert.Contains("name=\"Input.Right.Sphere\"", html);
        Assert.Contains("step=\"0.001\"", html);
        var values = new Dictionary<string, string>
        {
            ["Input.SubmissionId"] = Value(html, "Input.SubmissionId"), ["Input.Kind"] = "0",
            ["Input.Date"] = "2026-10-15", ["Input.ProfessionalId"] = professional.ToString(),
            ["Input.Source"] = "Documento ficticio", ["Input.Right.Sphere"] = "1.125", ["Input.Right.Cylinder"] = "0",
            ["Input.Left.Sphere"] = "-2.375", ["Input.PupillaryDistance"] = "62.25"
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsync(path, new FormUrlEncodedContent(values))).StatusCode);
        var saved = await BrowserForms.PostAsync(browser, path, path, values);
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        var detailsPath = saved.Headers.Location!.ToString();
        var details = WebUtility.HtmlDecode(await browser.GetStringAsync(detailsPath));
        Assert.Contains("1,125", details); Assert.Contains("-2,375", details); Assert.Contains("62,25", details);
        var id = Guid.Parse(detailsPath.Split('/').Last());
        var correctionPath = path + "/" + id;
        var edit = await browser.GetStringAsync(correctionPath);
        Assert.Contains("value=\"1.125\"", edit);
        values["Input.SubmissionId"] = Value(edit, "Input.SubmissionId");
        values["Input.Version"] = Value(edit, "Input.Version");
        values["Input.Right.Sphere"] = "1.375";
        // El motivo es obligatorio y la operación inválida no crea una revisión.
        Assert.Equal(HttpStatusCode.OK, (await BrowserForms.PostAsync(browser, correctionPath, correctionPath, values)).StatusCode);
        Assert.Equal(1, await scenario.Db.ClinicalRevisions.CountAsync());
        values["Input.CorrectionReason"] = "Corrección ficticia";
        Assert.Equal(HttpStatusCode.Redirect, (await BrowserForms.PostAsync(browser, correctionPath, correctionPath, values)).StatusCode);
        Assert.Equal(2, await scenario.Db.ClinicalRevisions.CountAsync());
        Assert.Contains("1,125", WebUtility.HtmlDecode(await browser.GetStringAsync(detailsPath + "?revision=1")));
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/clientes/{Guid.NewGuid()}/clinica/{id}")).StatusCode);
    }

    [Theory]
    [InlineData("admin.ficticio")]
    [InlineData("ventas.ficticio")]
    public async Task ClinicalDataCannotBeReadOrChangedByNonClinicalAccounts(string name)
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var scenario = await ClinicalScenario.OpenAsync(app, name);
        var id = Guid.NewGuid();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scenario.Clinical.ListAsync(id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scenario.Clinical.GetAsync(id, id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scenario.Clinical.CreateAsync(id, new()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scenario.Clinical.CorrectAsync(id, id, new()));
        using var browser = app.Browser();
        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync($"/clientes/{id}/clinica")).StatusCode);
        await BrowserForms.LoginAsync(browser, name);
        foreach (var path in new[] { $"/clientes/{id}/clinica", $"/clientes/{id}/clinica/nuevo", $"/clientes/{id}/clinica/{id}" })
        {
            var response = await browser.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/cuenta/denegado", response.Headers.Location!.ToString());
        }
        Assert.Equal(HttpStatusCode.Redirect, (await BrowserForms.PostAsync(browser, "/", $"/clientes/{id}/clinica/nuevo", new())).StatusCode);
        Assert.Empty(await scenario.Db.ClinicalRecords.ToListAsync());
    }

    private static string Value(string html, string name)
    {
        foreach (Match tag in Regex.Matches(html, "<input\\b[^>]*>"))
            if (tag.Value.Contains("name=\"" + name + "\""))
                return WebUtility.HtmlDecode(Regex.Match(tag.Value, "value=\"([^\"]*)\"").Groups[1].Value);
        throw new InvalidOperationException("Campo ausente: " + name);
    }
}
