using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Clinical;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Clinical;

public sealed partial class ClinicalAttachmentService(IDbContextFactory<OpticaDbContext> factory, ICurrentUser current,
    IPrivateAttachmentStore store, IClinicalFilePolicyService policy, ClinicalFileValidation validation,
    IStorageCapacity capacity, IOptions<ClinicalFileOptions> options, ClinicalFileGate gate, TimeProvider clock) : IClinicalAttachmentService
{
    private static IQueryable<ClinicalAttachment> Scoped(OpticaDbContext db, Guid customerId, Guid recordId) =>
        from attachment in db.ClinicalAttachments
        join revision in db.ClinicalRevisions on attachment.RevisionId equals revision.Id
        join record in db.ClinicalRecords on revision.RecordId equals record.Id
        where record.Id == recordId && record.CustomerId == customerId
        select attachment;

    public async Task<AttachmentPage> ListAsync(Guid customerId, Guid recordId, Guid revisionId, int page = 1, CancellationToken ct = default)
    {
        var actor = ClinicalOperations.Demand(current, Permissions.ReadClinical);
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await Scoped(db, customerId, recordId).AsNoTracking().Where(x => x.RevisionId == revisionId)
            .OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(HistoryPaging.Offset(page)).Take(HistoryPaging.Size + 1)
            .Select(x => new AttachmentDetails(x.Id, x.OriginalName, x.ContentType, x.Length, x.Sha256, x.CreatedAtUtc, x.State, x.Version, x.RetirementReason)).ToListAsync(ct);
        ClinicalOperations.Audit(db, actor, "clinical.attachments.read", recordId);
        await db.SaveChangesAsync(ct);
        return new(rows.Take(HistoryPaging.Size).ToArray(), page, rows.Count > HistoryPaging.Size);
    }

    public async Task<AttachmentDownload?> DownloadAsync(Guid customerId, Guid recordId, Guid attachmentId, CancellationToken ct = default)
    {
        var actor = ClinicalOperations.Demand(current, Permissions.ReadClinical);
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await Scoped(db, customerId, recordId).AsNoTracking().SingleOrDefaultAsync(x => x.Id == attachmentId, ct);
        if (row is null || row.State is not (AttachmentState.Available or AttachmentState.Retired)) return null;
        Stream? content = null;
        try
        {
            content = await store.OpenReadAsync(row.Id, ct);
            if (content is null || content.Length != row.Length ||
                Convert.ToHexString(await SHA256.HashDataAsync(content, ct)) != row.Sha256)
                throw new InvalidDataException();
            content.Position = 0;
            ClinicalOperations.Audit(db, actor, "clinical.attachment.downloaded", row.Id);
            await db.SaveChangesAsync(ct);
            return new(content, row.OriginalName, row.ContentType);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            if (content is not null) await content.DisposeAsync();
            ClinicalOperations.Audit(db, actor, "clinical.attachment.integrity-failed", row.Id);
            await db.SaveChangesAsync(ct);
            throw new InvalidDataException("No se pudo verificar el documento original. No se entrega una copia incompleta o alterada; revisá el almacenamiento y el respaldo.");
        }
        catch { if (content is not null) await content.DisposeAsync(); throw; }
    }

    public async Task<ClinicalResult> RetireAsync(Guid customerId, Guid recordId, Guid attachmentId, Guid version, string reason, CancellationToken ct = default)
    {
        var actor = ClinicalOperations.Demand(current, Permissions.WriteClinical);
        await gate.Semaphore.WaitAsync(ct);
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var row = await Scoped(db, customerId, recordId).SingleOrDefaultAsync(x => x.Id == attachmentId, ct);
            if (row is null) return ClinicalOperations.Missing();
            if (row.Version != version) return ClinicalOperations.Conflict();
            try { row.Retire(reason, actor, clock.GetUtcNow().UtcDateTime); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return ClinicalOperations.Invalid(ex.Message); }
            ClinicalOperations.Audit(db, actor, "clinical.attachment.retired", row.Id);
            return await ClinicalOperations.SaveAsync(db, row.Id, ct);
        }
        finally { gate.Semaphore.Release(); }
    }
}
