using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Clinical;

namespace OpticaSosa.Infrastructure.Clinical;

public sealed partial class ClinicalAttachmentService
{
    public async Task<ClinicalResult> RecoverAsync(Guid customerId, Guid recordId, Guid attachmentId, Guid version, CancellationToken ct = default)
    {
        var actor = ClinicalOperations.Demand(current, Permissions.WriteClinical);
        await gate.Semaphore.WaitAsync(ct);
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var row = await Scoped(db, customerId, recordId).SingleOrDefaultAsync(x => x.Id == attachmentId, ct);
            if (row is null) return ClinicalOperations.Missing();
            if (row.Version != version || row.State is not (AttachmentState.Pending or AttachmentState.Failed)) return ClinicalOperations.Conflict();
            try
            {
                await using var original = await store.OpenReadAsync(row.Id, ct);
                if (original is null || original.Length != row.Length || Convert.ToHexString(await SHA256.HashDataAsync(original, ct)) != row.Sha256)
                    return ClinicalOperations.Invalid("No hay un original completo verificable para recuperar. Seleccioná el archivo original y volvé a adjuntarlo; no se publica una copia parcial.");
                row.MarkAvailable();
                ClinicalOperations.Audit(db, actor, "clinical.attachment.recovered", row.Id);
                return await ClinicalOperations.SaveAsync(db, row.Id, ct);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return ClinicalOperations.Invalid("No se pudo verificar el archivo privado. Revisá el almacenamiento antes de reintentar."); }
        }
        finally { gate.Semaphore.Release(); }
    }
}
