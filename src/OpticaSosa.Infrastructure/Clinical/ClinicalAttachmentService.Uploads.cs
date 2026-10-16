using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Clinical;

namespace OpticaSosa.Infrastructure.Clinical;

public sealed partial class ClinicalAttachmentService
{
    public async Task<ClinicalResult> UploadAsync(Guid customerId, Guid recordId, AttachmentUpload upload, CancellationToken ct = default)
    {
        var actor = ClinicalOperations.Demand(current, Permissions.WriteClinical);
        if (upload.SubmissionId == Guid.Empty) return ClinicalOperations.Invalid("Recargá el formulario.");
        await gate.Semaphore.WaitAsync(ct);
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var root = await db.ClinicalRecords.SingleOrDefaultAsync(x => x.Id == recordId && x.CustomerId == customerId, ct);
            if (root is null || !await db.ClinicalRevisions.AnyAsync(x => x.Id == upload.RevisionId && x.RecordId == recordId, ct))
                return ClinicalOperations.Missing();
            string name, type; byte[] bytes;
            try
            {
                name = ClinicalFileValidation.SafeName(upload.OriginalName);
                type = ClinicalFileValidation.ExpectedType(name);
                var limits = await policy.GetAsync(ct);
                var limit = Math.Min((type == "application/pdf" ? limits.PdfMiB : limits.PhotoMiB) * ClinicalFileOptions.MiB,
                    options.Value.RequestMaxBytes - ClinicalFileOptions.MiB);
                bytes = await validation.ReadAsync(upload, limit, ct);
                validation.Validate(bytes, type, upload.ContentType);
            }
            catch (ArgumentException ex)
            {
                ClinicalOperations.Audit(db, actor, "clinical.attachment.rejected", recordId);
                await db.SaveChangesAsync(ct);
                return ClinicalOperations.Invalid(ex.Message);
            }
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            var row = await db.ClinicalAttachments.FindAsync([upload.SubmissionId], ct);
            if (row is not null)
            {
                if (row.RevisionId != upload.RevisionId || row.CreatedByUserId != actor || row.OriginalName != name || row.Sha256 != hash)
                    return ClinicalOperations.Conflict();
                if (row.State == AttachmentState.Available) return new(ClinicalResultCode.Success, row.Id);
                if (row.State == AttachmentState.Retired) return ClinicalOperations.Conflict();
                row.Retry();
            }
            else
            {
                if (root.Version != upload.RecordVersion) return ClinicalOperations.Conflict();
                row = new(upload.SubmissionId, upload.RevisionId, name, type, bytes.LongLength, hash, actor, clock.GetUtcNow().UtcDateTime);
                db.Add(row);
            }
            if (capacity.AvailableBytes - bytes.LongLength < options.Value.ReserveDiskMiB * ClinicalFileOptions.MiB)
                return ClinicalOperations.Invalid("No hay espacio suficiente en el servidor manteniendo la reserva de seguridad. No se guardó el archivo; avisá al administrador.");
            root.Touch();
            ClinicalOperations.Audit(db, actor, "clinical.attachment.pending", row.Id);
            var reserved = await ClinicalOperations.SaveAsync(db, row.Id, ct);
            if (!reserved.Succeeded) return reserved;
            try
            {
                // Pending es durable antes de publicar bytes: aun ante un corte hay metadatos
                // para el archivo. El reenvío del mismo ID/contenido recupera la operación.
                await using var existing = await store.OpenReadAsync(row.Id, ct);
                if (existing is null) await store.WriteAsync(row.Id, bytes, ct);
                else if (existing.Length != row.Length || Convert.ToHexString(await SHA256.HashDataAsync(existing, ct)) != hash)
                    throw new IOException("El archivo existente no coincide.");
                row.MarkAvailable();
                ClinicalOperations.Audit(db, actor, "clinical.attachment.created", row.Id);
                return await ClinicalOperations.SaveAsync(db, row.Id, ct);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                row.MarkFailed();
                ClinicalOperations.Audit(db, actor, "clinical.attachment.failed", row.Id);
                await ClinicalOperations.SaveAsync(db, row.Id, CancellationToken.None);
                return ClinicalOperations.Invalid("La carga no se completó. El original no se modificó. Podés reenviar el mismo formulario para reintentar.");
            }
        }
        finally { gate.Semaphore.Release(); }
    }
}
