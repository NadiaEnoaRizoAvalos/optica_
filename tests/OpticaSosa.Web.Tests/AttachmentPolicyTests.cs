using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Clinical;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class AttachmentPolicyTests
{
    [Fact]
    public async Task AdministratorCanConfigureSizesWithConcurrencyAndLoweringThemDoesNotBlockExistingDownloads()
    {
        await using var test = await AttachmentTestContext.CreateAsync();
        var saved = await test.UploadAsync(AttachmentTestContext.Pdf(), "documento-ficticio.pdf", "application/pdf");
        Assert.True(saved.Succeeded);
        var initial = await test.Scenario.Policy.GetAsync(); Assert.Equal(25, initial.PhotoMiB); Assert.Equal(50, initial.PdfMiB);
        using var admin = await ClinicalScenario.OpenAsync(test.App, "admin.ficticio");
        Assert.True((await admin.Policy.SaveAsync(1, 1, initial.Version)).Succeeded);
        Assert.Equal(ClinicalResultCode.Conflict, (await admin.Policy.SaveAsync(2, 2, initial.Version)).Code);
        var now = await test.Scenario.Policy.GetAsync(); Assert.Equal(1, now.PdfMiB);
        var d = (await test.Scenario.Clinical.GetAsync(test.CustomerId, test.RecordId))!;
        using var empty = new MemoryStream();
        var tooLarge = await test.Scenario.Attachments.UploadAsync(test.CustomerId, test.RecordId,
            new(Guid.NewGuid(), d.RevisionId, d.Version, "grande-ficticio.pdf", "application/pdf", ClinicalFileOptions.MiB + 1, empty));
        Assert.False(tooLarge.Succeeded); Assert.Contains("1 MB", tooLarge.Error!);
        Assert.Equal(AttachmentTestContext.Pdf(), await test.DownloadAsync(saved.Id!.Value));
        Assert.False((await admin.Policy.SaveAsync(now.TechnicalRequestMiB, 1, now.Version)).Succeeded);
        Assert.True(await test.Scenario.Db.AuditEntries.AnyAsync(x => x.Action == "clinical.file-policy.updated"));
    }
    [Fact]
    public async Task ProfessionalsCannotRaiseLimitsAndSalesCannotReadThem()
    {
        await using var test = await AttachmentTestContext.CreateAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => test.Scenario.Policy.SaveAsync(80, 80, Guid.Empty));
        using var sales = await ClinicalScenario.OpenAsync(test.App, "ventas.ficticio");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sales.Policy.GetAsync());
    }
}
