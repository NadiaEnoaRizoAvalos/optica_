using System.Text;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Clinical;
using Xunit;

namespace OpticaSosa.Web.Tests;

internal sealed class AttachmentTestContext(SecurityApp app, ClinicalScenario scenario, Guid customerId, Guid recordId) : IAsyncDisposable
{
    public SecurityApp App { get; } = app;
    public ClinicalScenario Scenario { get; } = scenario;
    public Guid CustomerId { get; } = customerId;
    public Guid RecordId { get; } = recordId;
    public static async Task<AttachmentTestContext> CreateAsync()
    {
        var app = new SecurityApp(); await app.InitializeAsync();
        using var customers = await CustomerScenario.OpenAsync(app);
        var customerId = (await customers.Customers.CreateAsync(CustomerScenario.Input())).CustomerId!.Value;
        var scenario = await ClinicalScenario.OpenAsync(app);
        var professional = await scenario.AddProfessionalAsync();
        var result = await scenario.Clinical.CreateAsync(customerId, ClinicalScenario.Input(professional));
        Assert.True(result.Succeeded);
        return new(app, scenario, customerId, result.Id!.Value);
    }
    public async Task<ClinicalResult> UploadAsync(byte[]? bytes = null, string name = "receta-ficticia.png", string type = "image/png",
        IClinicalAttachmentService? service = null, Guid? submissionId = null)
    {
        bytes ??= BusinessForms.Logo();
        var d = (await Scenario.Clinical.GetAsync(CustomerId, RecordId))!;
        using var input = new MemoryStream(bytes);
        return await (service ?? Scenario.Attachments).UploadAsync(CustomerId, RecordId,
            new(submissionId ?? Guid.NewGuid(), d.RevisionId, d.Version, name, type, bytes.Length, input));
    }
    public async Task<byte[]> DownloadAsync(Guid id)
    {
        var file = (await Scenario.Attachments.DownloadAsync(CustomerId, RecordId, id))!;
        await using var content = file.Content;
        using var output = new MemoryStream(); await content.CopyToAsync(output); return output.ToArray();
    }
    public async ValueTask DisposeAsync() { Scenario.Dispose(); await App.DisposeAsync(); }

    public static byte[] Pdf(int padding = 0)
    {
        // PDF ficticio mínimo con xref correcto; el stream opcional permite probar tamaños reales.
        using var output = new MemoryStream();
        void Write(string value) => output.Write(Encoding.ASCII.GetBytes(value));
        Write("%PDF-1.4\n"); var offsets = new List<long> { 0 };
        void Obj(int id, string text) { offsets.Add(output.Position); Write($"{id} 0 obj\n{text}\nendobj\n"); }
        Obj(1, "<< /Type /Catalog /Pages 2 0 R >>");
        Obj(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Obj(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>");
        if (padding > 0)
        {
            offsets.Add(output.Position); Write($"4 0 obj\n<< /Length {padding} >>\nstream\n");
            var block = new byte[81920]; int left = padding;
            while (left > 0) { var count = Math.Min(left, block.Length); output.Write(block, 0, count); left -= count; }
            Write("\nendstream\nendobj\n");
        }
        var xref = output.Position; Write($"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) Write(offset.ToString("D10") + " 00000 n \n");
        Write($"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }
}
