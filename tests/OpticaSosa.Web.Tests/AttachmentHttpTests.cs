using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Clinical;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class AttachmentHttpTests
{
    [Fact]
    public async Task ProfessionalCanUploadAndDownloadOriginalWithCsrfAndPrivateResponseHeaders()
    {
        await using var test = await AttachmentTestContext.CreateAsync();
        using var browser = test.App.Browser(); await BrowserForms.LoginAsync(browser, "profesional.ficticio");
        var path = $"/clientes/{test.CustomerId}/clinica/{test.RecordId}/adjuntos";
        var html = await browser.GetStringAsync(path);
        Assert.Contains("25 MB", html); Assert.Contains("50 MB", html);
        var fields = ReadFields(html);
        using (var forged = Form(fields, BusinessForms.Logo(), false))
            Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsync(path, forged)).StatusCode);
        using (var upload = Form(fields, BusinessForms.Logo(), true))
            Assert.Equal(HttpStatusCode.Redirect, (await browser.PostAsync(path, upload)).StatusCode);
        var row = await test.Scenario.Db.ClinicalAttachments.AsNoTracking().SingleAsync();
        var downloadPath = path + $"/{row.Id}/descargar";
        var result = await browser.GetAsync(downloadPath);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("attachment", result.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("image/png", result.Content.Headers.ContentType!.MediaType);
        Assert.Equal("nosniff", result.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("no-store", result.Headers.CacheControl!.ToString());
        Assert.Equal(BusinessForms.Logo(), await result.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/clientes/{Guid.NewGuid()}/clinica/{test.RecordId}/adjuntos/{row.Id}/descargar")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/attachments/clinical/{row.Id:N}.bin")).StatusCode);
        using var anonymous = test.App.Browser();
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync(downloadPath)).StatusCode);
        using var admin = test.App.Browser(); await BrowserForms.LoginAsync(admin, "admin.ficticio");
        var denied = await admin.GetAsync(downloadPath);
        Assert.Contains("/cuenta/denegado", denied.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData("admin.ficticio")]
    [InlineData("ventas.ficticio")]
    public async Task NonClinicalRolesCannotCallDocumentUseCasesOrEndpoints(string user)
    {
        await using var test = await AttachmentTestContext.CreateAsync();
        using var actor = await ClinicalScenario.OpenAsync(test.App, user);
        var id = Guid.NewGuid(); using var input = new MemoryStream(BusinessForms.Logo());
        var request = new AttachmentUpload(id, id, id, "ficticia.png", "image/png", input.Length, input);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => actor.Attachments.ListAsync(test.CustomerId, test.RecordId, id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => actor.Attachments.UploadAsync(test.CustomerId, test.RecordId, request));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => actor.Attachments.DownloadAsync(test.CustomerId, test.RecordId, id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => actor.Attachments.RetireAsync(test.CustomerId, test.RecordId, id, id, "Ficticio"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => actor.Attachments.RecoverAsync(test.CustomerId, test.RecordId, id, id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => actor.Attachments.OptimizeAsync(test.CustomerId, test.RecordId, request));
        using var browser = test.App.Browser(); await BrowserForms.LoginAsync(browser, user);
        var path = $"/clientes/{test.CustomerId}/clinica/{test.RecordId}/adjuntos";
        var response = await browser.GetAsync(path);
        Assert.Contains("/cuenta/denegado", response.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.Redirect, (await BrowserForms.PostAsync(browser, "/", path, new())).StatusCode);
        Assert.Empty(await test.Scenario.Db.ClinicalAttachments.ToListAsync());
    }

    [Fact]
    public async Task OnlyAdministratorCanChangeSizeLimitsAndCreateCopiesThroughProtectedForms()
    {
        await using var test = await AttachmentTestContext.CreateAsync();
        using var professional = test.App.Browser(); await BrowserForms.LoginAsync(professional, "profesional.ficticio");
        var denied = await professional.GetAsync("/admin/archivos-clinicos");
        Assert.Contains("/cuenta/denegado", denied.Headers.Location!.ToString());
        using var admin = test.App.Browser(); await BrowserForms.LoginAsync(admin, "admin.ficticio");
        var html = await admin.GetStringAsync("/admin/archivos-clinicos");
        var version = ReadFields(html)["Version"];
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/admin/archivos-clinicos", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await BrowserForms.PostAsync(admin, "/admin/archivos-clinicos", "/admin/archivos-clinicos",
            new() { ["Version"] = version, ["PhotoMiB"] = "30", ["PdfMiB"] = "60" })).StatusCode);
        Assert.Equal(30, (await test.Scenario.Policy.GetAsync()).PhotoMiB);
        Assert.Equal(HttpStatusCode.Redirect, (await BrowserForms.PostAsync(admin, "/admin/archivos-clinicos", "/admin/archivos-clinicos?handler=Backup", new())).StatusCode);
    }

    internal static Dictionary<string, string> ReadFields(string html)
    {
        var result = new Dictionary<string, string>();
        foreach (Match tag in Regex.Matches(html, "<input\\b[^>]*>"))
        {
            var name = Regex.Match(tag.Value, "\\bname=\"([^\"]+)\"").Groups[1].Value;
            var value = Regex.Match(tag.Value, "\\bvalue=\"([^\"]*)\"").Groups[1].Value;
            if (tag.Value.Contains("type=\"hidden\"") && name != "__Invariant") result[name] = WebUtility.HtmlDecode(value);
        }
        return result;
    }
    internal static MultipartFormDataContent Form(Dictionary<string, string> fields, byte[] bytes, bool token,
        string fileName = "receta-ficticia.png", string type = "image/png")
    {
        var body = new MultipartFormDataContent();
        foreach (var field in fields)
            if (token || field.Key != "__RequestVerificationToken") body.Add(new StringContent(field.Value), field.Key);
        var content = new ByteArrayContent(bytes); content.Headers.ContentType = new MediaTypeHeaderValue(type);
        body.Add(content, "Document", fileName); return body;
    }
}
