using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Infrastructure.Persistence;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class AttachmentBackupTests
{
    [Fact]
    public async Task SnapshotRestoresOriginalsAndTheirRelationsIntoASeparateServer()
    {
        await using var test = await AttachmentTestContext.CreateAsync();
        var png = await test.UploadAsync();
        var pdf = await test.UploadAsync(AttachmentTestContext.Pdf(), "receta-ficticia.pdf", "application/pdf");
        Assert.True(png.Succeeded); Assert.True(pdf.Succeeded);
        var pngRow = await test.Scenario.Db.ClinicalAttachments.AsNoTracking().SingleAsync(x => x.Id == png.Id);
        Assert.True((await test.Scenario.Attachments.RetireAsync(test.CustomerId, test.RecordId, pngRow.Id, pngRow.Version, "Retiro ficticio")).Succeeded);
        using var admin = await ClinicalScenario.OpenAsync(test.App, "admin.ficticio");
        var copy = await admin.Snapshots.CreateAsync();
        Assert.Equal("Complete", copy.Status); Assert.Equal(2, copy.Files); Assert.True(File.Exists(Path.Combine(copy.Directory, "complete.json")));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => test.Scenario.Snapshots.CreateAsync());
        await using var restored = new SecurityApp();
        using (var scope = restored.Services.CreateScope())
        {
            var paths = scope.ServiceProvider.GetRequiredService<StoragePaths>();
            Assert.Equal(Path.Combine(restored.Root, "private"), paths.DataDirectory);
            File.Copy(Path.Combine(copy.Directory, "optica.db"), paths.DatabaseFile, overwrite: false);
            var files = Path.Combine(copy.Directory, "attachments", "clinical");
            Directory.CreateDirectory(Path.Combine(paths.AttachmentsDirectory, "clinical"));
            foreach (var file in Directory.GetFiles(files)) File.Copy(file, Path.Combine(paths.AttachmentsDirectory, "clinical", Path.GetFileName(file)));
        }
        using var recovered = await ClinicalScenario.OpenAsync(restored);
        var original = await recovered.Attachments.DownloadAsync(test.CustomerId, test.RecordId, pdf.Id!.Value);
        Assert.NotNull(original); await using var content = original.Content;
        using var buffer = new MemoryStream(); await content.CopyToAsync(buffer);
        Assert.Equal(AttachmentTestContext.Pdf(), buffer.ToArray());
        Assert.NotNull(await recovered.Clinical.GetAsync(test.CustomerId, test.RecordId));
    }

    [Fact]
    public async Task FailedSnapshotDoesNotOverwriteThePreviousCompleteCopy()
    {
        await using var test = await AttachmentTestContext.CreateAsync();
        var result = await test.UploadAsync(); Assert.True(result.Succeeded);
        using var admin = await ClinicalScenario.OpenAsync(test.App, "admin.ficticio");
        var good = await admin.Snapshots.CreateAsync();
        using var scope = test.App.Services.CreateScope();
        var paths = scope.ServiceProvider.GetRequiredService<StoragePaths>();
        await File.WriteAllBytesAsync(Path.Combine(paths.AttachmentsDirectory, "clinical", result.Id!.Value.ToString("N") + ".bin"), "Alteración ficticia"u8.ToArray());
        await Assert.ThrowsAsync<IOException>(() => admin.Snapshots.CreateAsync());
        Assert.True(File.Exists(Path.Combine(good.Directory, "complete.json")));
        Assert.Equal(BusinessForms.Logo(), await File.ReadAllBytesAsync(Path.Combine(good.Directory, "attachments", "clinical", result.Id.Value.ToString("N") + ".bin")));
        Assert.True(await test.Scenario.Db.AuditEntries.AnyAsync(x => x.Action == "clinical.snapshot.failed"));
    }
}
