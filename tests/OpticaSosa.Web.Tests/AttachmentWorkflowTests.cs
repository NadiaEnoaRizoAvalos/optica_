using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Domain.Clinical;
using OpticaSosa.Infrastructure.Persistence;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class AttachmentWorkflowTests
{
    [Fact]
    public async Task MoreThanTenDocumentsRemainIndependentAndPaginatedWithoutReplacingOriginals()
    {
        await using var test = await AttachmentTestContext.CreateAsync();
        var bytes = BusinessForms.Logo(); var expected = Convert.ToHexString(SHA256.HashData(bytes));
        for (int n = 0; n < 32; n++) Assert.True((await test.UploadAsync(bytes, $"receta-ficticia-{n}.png")).Succeeded);
        var details = (await test.Scenario.Clinical.GetAsync(test.CustomerId, test.RecordId))!;
        var first = await test.Scenario.Attachments.ListAsync(test.CustomerId, test.RecordId, details.RevisionId);
        var next = await test.Scenario.Attachments.ListAsync(test.CustomerId, test.RecordId, details.RevisionId, 2);
        Assert.Equal(25, first.Items.Count); Assert.True(first.HasMore); Assert.Equal(7, next.Items.Count); Assert.False(next.HasMore);
        Assert.Equal(32, first.Items.Concat(next.Items).Select(x => x.Id).Distinct().Count());
        Assert.All(first.Items.Concat(next.Items), file => Assert.Equal(expected, file.Sha256));
        Assert.Equal(bytes, await test.DownloadAsync(first.Items[0].Id));
        Assert.Equal(32, await test.Scenario.Db.ClinicalAttachments.CountAsync());
    }

    [Fact]
    public async Task InvalidContentAndCrossCustomerLinksDoNotCreateDocuments()
    {
        await using var test = await AttachmentTestContext.CreateAsync();
        Assert.False((await test.UploadAsync("%PDF-falso"u8.ToArray(), "falso.pdf", "application/pdf")).Succeeded);
        Assert.False((await test.UploadAsync("<script>falso</script>"u8.ToArray())).Succeeded);
        Assert.False((await test.UploadAsync(BusinessForms.Logo(), "falso.jpg", "image/jpeg")).Succeeded);
        Assert.False((await test.UploadAsync(BusinessForms.Logo(), "no-admitido.heic", "image/heic")).Succeeded);
        var d = (await test.Scenario.Clinical.GetAsync(test.CustomerId, test.RecordId))!;
        using var input = new MemoryStream(BusinessForms.Logo());
        Assert.False((await test.Scenario.Attachments.UploadAsync(Guid.NewGuid(), test.RecordId,
            new(Guid.NewGuid(), d.RevisionId, d.Version, "ficticia.png", "image/png", input.Length, input))).Succeeded);
        Assert.Empty(await test.Scenario.Db.ClinicalAttachments.ToListAsync());
    }

    [Fact]
    public async Task IdempotencyRetirementAndTamperDetectionPreserveMetadataAndAudit()
    {
        await using var test = await AttachmentTestContext.CreateAsync();
        var id = Guid.NewGuid(); var bytes = BusinessForms.Logo();
        Assert.True((await test.UploadAsync(bytes, submissionId: id)).Succeeded);
        Assert.True((await test.UploadAsync(bytes, submissionId: id)).Succeeded);
        Assert.Single(await test.Scenario.Db.ClinicalAttachments.ToListAsync());
        Assert.Null(await test.Scenario.Attachments.DownloadAsync(Guid.NewGuid(), test.RecordId, id));
        var row = await test.Scenario.Db.ClinicalAttachments.AsNoTracking().SingleAsync();
        Assert.False((await test.Scenario.Attachments.RetireAsync(test.CustomerId, test.RecordId, id, Guid.NewGuid(), "Motivo ficticio")).Succeeded);
        Assert.True((await test.Scenario.Attachments.RetireAsync(test.CustomerId, test.RecordId, id, row.Version, "Documento retirado ficticio")).Succeeded);
        Assert.Equal(bytes, await test.DownloadAsync(id));
        using var scope = test.App.Services.CreateScope();
        var paths = scope.ServiceProvider.GetRequiredService<StoragePaths>();
        await File.WriteAllBytesAsync(Path.Combine(paths.AttachmentsDirectory, "clinical", id.ToString("N") + ".bin"), "Archivo alterado ficticio"u8.ToArray());
        await Assert.ThrowsAsync<InvalidDataException>(() => test.Scenario.Attachments.DownloadAsync(test.CustomerId, test.RecordId, id));
        var audit = await test.Scenario.Db.AuditEntries.Where(x => x.Action.StartsWith("clinical.attachment.")).ToListAsync();
        Assert.Single(audit, x => x.Action == "clinical.attachment.created");
        Assert.Contains(audit, x => x.Action == "clinical.attachment.retired");
        Assert.Contains(audit, x => x.Action == "clinical.attachment.integrity-failed");
        Assert.DoesNotContain(audit, x => (x.TargetId ?? "").Contains("receta-ficticia"));
    }

    [Fact]
    public async Task LowDiskAndInterruptedWritesDoNotPublishPartialFilesAndRetryCanRecover()
    {
        await using var test = await AttachmentTestContext.CreateAsync();
        Assert.False((await test.UploadAsync(service: test.Scenario.AttachmentsWith(capacity: new NoSpace()))).Succeeded);
        Assert.Empty(await test.Scenario.Db.ClinicalAttachments.ToListAsync());
        using var scope = test.App.Services.CreateScope();
        var realStore = scope.ServiceProvider.GetRequiredService<IPrivateAttachmentStore>();
        var id = Guid.NewGuid();
        Assert.False((await test.UploadAsync(service: test.Scenario.AttachmentsWith(store: new FailingStore(realStore)), submissionId: id)).Succeeded);
        Assert.Equal(AttachmentState.Failed, (await test.Scenario.Db.ClinicalAttachments.AsNoTracking().SingleAsync()).State);
        Assert.Null(await test.Scenario.Attachments.DownloadAsync(test.CustomerId, test.RecordId, id));
        Assert.True((await test.UploadAsync(submissionId: id)).Succeeded);
        Assert.Equal(BusinessForms.Logo(), await test.DownloadAsync(id));
    }

    private sealed class NoSpace : IStorageCapacity { public long AvailableBytes => 0; }
    [Fact]
    public async Task FailureAfterPublishingBytesLeavesDurablePendingMetadataAndRetryCompletesWithoutOverwriting()
    {
        await using var test = await AttachmentTestContext.CreateAsync();
        // Trigger exclusivamente en la base ficticia aislada; simula fallo en la segunda confirmación.
        await test.Scenario.Db.Database.ExecuteSqlRawAsync("CREATE TRIGGER SyntheticFailCompletion BEFORE UPDATE ON ClinicalAttachments WHEN NEW.State = 1 BEGIN SELECT RAISE(ABORT, 'synthetic failure'); END;");
        var id = Guid.NewGuid();
        Assert.False((await test.UploadAsync(submissionId: id)).Succeeded);
        Assert.Equal(AttachmentState.Pending, (await test.Scenario.Db.ClinicalAttachments.AsNoTracking().SingleAsync()).State);
        Assert.Null(await test.Scenario.Attachments.DownloadAsync(test.CustomerId, test.RecordId, id));
        await test.Scenario.Db.Database.ExecuteSqlRawAsync("DROP TRIGGER SyntheticFailCompletion;");
        var pending = await test.Scenario.Db.ClinicalAttachments.AsNoTracking().SingleAsync();
        Assert.True((await test.Scenario.Attachments.RecoverAsync(test.CustomerId, test.RecordId, id, pending.Version)).Succeeded);
        Assert.Equal(BusinessForms.Logo(), await test.DownloadAsync(id));
        Assert.Single(await test.Scenario.Db.ClinicalAttachments.AsNoTracking().ToListAsync());
    }
    private sealed class FailingStore(IPrivateAttachmentStore inner) : IPrivateAttachmentStore
    {
        public Task WriteAsync(Guid id, byte[] bytes, CancellationToken ct = default) => throw new IOException("Fallo ficticio");
        public Task<byte[]?> ReadAsync(Guid id, CancellationToken ct = default) => inner.ReadAsync(id, ct);
        public Task<Stream?> OpenReadAsync(Guid id, CancellationToken ct = default) => inner.OpenReadAsync(id, ct);
    }
}
