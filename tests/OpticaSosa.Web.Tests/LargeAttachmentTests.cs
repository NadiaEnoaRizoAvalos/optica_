using OpticaSosa.Application.Clinical;
using Xunit;

namespace OpticaSosa.Web.Tests;

[CollectionDefinition("Large files", DisableParallelization = true)]
public sealed class LargeFileCollection;

[Collection("Large files")]
public sealed class LargeAttachmentTests
{
    [Fact]
    public async Task PdfAboveFormerThirtyTwoMegabyteReadLimitIsStoredAndDownloadedWithoutChangingItsBytes()
    {
        await using var test = await AttachmentTestContext.CreateAsync();
        var original = AttachmentTestContext.Pdf(33 * (int)ClinicalFileOptions.MiB);
        Assert.True(original.Length < 50 * ClinicalFileOptions.MiB);
        var result = await test.UploadAsync(original, "documento-grande-ficticio.pdf", "application/pdf");
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(original, await test.DownloadAsync(result.Id!.Value));
    }
    [Fact]
    public async Task ConfiguredPerFileLimitsRejectOversizedAndTruncatedStreamsBeforePublication()
    {
        await using var test = await AttachmentTestContext.CreateAsync();
        var d = (await test.Scenario.Clinical.GetAsync(test.CustomerId, test.RecordId))!;
        foreach (var (name, type, limit) in new[] { ("ficticia.png", "image/png", 25), ("ficticia.pdf", "application/pdf", 50) })
        {
            using var empty = new MemoryStream();
            var result = await test.Scenario.Attachments.UploadAsync(test.CustomerId, test.RecordId,
                new(Guid.NewGuid(), d.RevisionId, d.Version, name, type, limit * ClinicalFileOptions.MiB + 1, empty));
            Assert.False(result.Succeeded); Assert.Contains(limit + " MB", result.Error!);
        }
        using var shortStream = new MemoryStream(new byte[20]);
        Assert.False((await test.Scenario.Attachments.UploadAsync(test.CustomerId, test.RecordId,
            new(Guid.NewGuid(), d.RevisionId, d.Version, "incompleta.png", "image/png", 100, shortStream))).Succeeded);
    }
}
