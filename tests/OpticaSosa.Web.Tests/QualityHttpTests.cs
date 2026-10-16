using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Domain.Work;
using Xunit;
namespace OpticaSosa.Web.Tests;
public sealed class QualityHttpTests
{
    [Fact]
    public async Task GuidedMultipartUploadReturnsToHubAndPreservesDeclaredSymptomsAndOriginals()
    {
        // Preservación opcional exclusivamente para revisión visual aislada, nunca sobre data local.
        var visualRoot = Environment.GetEnvironmentVariable("OPTICA_VISUAL_ROOT");
        await using var app = new SecurityApp(visualRoot, preserveData: visualRoot is not null); await app.InitializeAsync();
        using var customers = await CustomerScenario.OpenAsync(app); var customer = (await customers.Customers.CreateAsync(CustomerScenario.Input("FIC-UX-001"))).CustomerId!.Value;
        using var pro = await ClinicalScenario.OpenAsync(app); var professional = await pro.AddProfessionalAsync();
        using var browser = app.Browser(); await BrowserForms.LoginAsync(browser, "profesional.ficticio");
        var path = $"/clientes/{customer}/clinica/nuevo"; var html = await browser.GetStringAsync(path);
        Assert.Contains("data-clinical-files", html); Assert.Contains("data-file-previews", html);
        using var content = new MultipartFormDataContent();
        foreach (var pair in new Dictionary<string, string> {
            ["__RequestVerificationToken"] = Value(html, "__RequestVerificationToken"), ["Input.SubmissionId"] = Value(html, "Input.SubmissionId"),
            ["RegistrationType"] = "medical", ["Input.Date"] = "2026-10-16", ["Input.ProfessionalId"] = professional.ToString(),
            ["Input.Source"] = "Documento externo ficticio", ["Input.ReportedSymptoms"] = "Motivo declarado ficticio", ["Input.ReportedBy"] = "0" }) content.Add(new StringContent(pair.Value), pair.Key);
        for (var n = 0; n < 12; n++) { var file = new ByteArrayContent(BusinessForms.Logo()); file.Headers.ContentType = new MediaTypeHeaderValue("image/png"); content.Add(file, "Documents", $"documento-ficticio-{n}.png"); }
        var saved = await browser.PostAsync(path, content); Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode); Assert.Equal($"/clientes/{customer}#recetas", saved.Headers.Location!.ToString());
        var hubResponse = await browser.GetAsync($"/clientes/{customer}");
        var hub = WebUtility.HtmlDecode(await hubResponse.Content.ReadAsStringAsync()); Assert.True(hubResponse.IsSuccessStatusCode, hub);
        Assert.Contains("Documentos adjuntados: 12", hub); Assert.Contains("Recetas y graduaciones", hub);
        var record = await pro.Db.ClinicalRecords.SingleAsync(); var detail = (await pro.Clinical.GetAsync(customer, record.Id))!;
        Assert.Equal("Motivo declarado ficticio", detail.Data.ReportedSymptoms); Assert.Equal(12, await pro.Db.ClinicalAttachments.CountAsync());
        foreach (var id in await pro.Db.ClinicalAttachments.Select(x => x.Id).ToListAsync()) {
            var download = (await pro.Attachments.DownloadAsync(customer, record.Id, id))!; await using var stream = download.Content; using var bytes = new MemoryStream(); await stream.CopyToAsync(bytes); Assert.Equal(BusinessForms.Logo(), bytes.ToArray());
        }
        var service = QualityWorkflowTests.Service(pro); var work = await service.CreateAsync(new() { CustomerId = customer, Product = "Anteojos ficticios para revisión", Frame = "Armazón de prueba", PrescriptionId = record.Id }); Assert.True(work.Succeeded);
        await QualityWorkflowTests.PrepareAsync(service, work.Id!.Value);
        if (visualRoot is not null) {
            await File.WriteAllTextAsync(Path.Combine(visualRoot, "visual.json"), JsonSerializer.Serialize(new { customer, professional, record = record.Id, work = work.Id }));
            await File.WriteAllBytesAsync(Path.Combine(visualRoot, "documento-ficticio.png"), BusinessForms.Logo());
        }
    }
    [Fact]
    public async Task DirectHttpCannotBypassApprovalAndSalesCannotOpenTechnicalScreen()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync(); using var pro = await ClinicalScenario.OpenAsync(app); var service = QualityWorkflowTests.Service(pro);
        var id = await QualityWorkflowTests.CreateAsync(app, pro); await QualityWorkflowTests.PrepareAsync(service, id);
        using var browser = app.Browser(); await BrowserForms.LoginAsync(browser, "profesional.ficticio");
        var path = $"/pedidos/{id}/control?etapa=0"; var html = await browser.GetStringAsync(path);
        Assert.Matches("data-approve[^>]*disabled|disabled[^>]*data-approve", html);
        var input = await QualityWorkflowTests.InputAsync(service, id, QualityStage.Technical); var values = new Dictionary<string, string> {
            ["Input.SubmissionId"] = input.SubmissionId.ToString(), ["Input.WorkVersion"] = input.WorkVersion.ToString(), ["Input.TemplateVersion"] = input.TemplateVersion.ToString(), ["Input.Stage"] = "0", ["Command"] = "approve" };
        for (var n = 0; n < input.Items.Count; n++) { values[$"Input.Items[{n}].Key"] = input.Items[n].Key; values[$"Input.Items[{n}].Status"] = n == 0 ? "0" : "1"; }
        var failed = await BrowserForms.PostAsync(browser, path, path, values); Assert.Equal(HttpStatusCode.OK, failed.StatusCode); Assert.Empty((await service.GetAsync(id))!.Controls);
        values["Input.Items[0].Status"] = "1"; Assert.Equal(HttpStatusCode.Redirect, (await BrowserForms.PostAsync(browser, path, path, values)).StatusCode); Assert.Single((await service.GetAsync(id))!.Controls);
        using var sales = app.Browser(); await BrowserForms.LoginAsync(sales, "ventas.ficticio");
        var denied = await sales.GetAsync(path); Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode); Assert.Contains("/cuenta/denegado", denied.Headers.Location!.ToString());
        Assert.DoesNotContain("Recetas y antecedentes", WebUtility.HtmlDecode(await sales.GetStringAsync("/")));
        Assert.Equal(HttpStatusCode.Redirect, (await sales.GetAsync("/administracion")).StatusCode);
        var work = (await service.GetAsync(id))!.Work;
        Assert.Equal(HttpStatusCode.OK, (await BrowserForms.PostAsync(sales, $"/pedidos/{id}", $"/pedidos/{id}", new() { ["Version"] = work.Version.ToString(), ["Command"] = "deliver" })).StatusCode);
        Assert.Equal(WorkStatus.PendingControl, (await service.GetAsync(id))!.Work.Status);
    }
    private static string Value(string html, string name)
    {
        foreach (Match tag in Regex.Matches(html, "<input\\b[^>]*>")) if (tag.Value.Contains("name=\"" + name + "\""))
            return WebUtility.HtmlDecode(Regex.Match(tag.Value, "value=\"([^\"]*)\"").Groups[1].Value);
        throw new InvalidOperationException("Campo ausente.");
    }
}
