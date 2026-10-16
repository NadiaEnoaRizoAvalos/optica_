using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Security;

namespace OpticaSosa.Infrastructure.Clinical;

public sealed partial class ClinicalAttachmentService
{
    public async Task<ImageOptimization> OptimizeAsync(Guid customerId, Guid recordId, AttachmentUpload upload, CancellationToken ct = default)
    {
        var actor = ClinicalOperations.Demand(current, Permissions.WriteClinical);
        await gate.Semaphore.WaitAsync(ct);
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            if (!await db.ClinicalRecords.AnyAsync(x => x.Id == recordId && x.CustomerId == customerId, ct))
                return new(null, null, "No se encontró el registro para este cliente.");
            try
            {
                var name = ClinicalFileValidation.SafeName(upload.OriginalName);
                var type = ClinicalFileValidation.ExpectedType(name);
                if (type != "image/png") return new(null, null, "La optimización sin pérdida está disponible para PNG. No se recomprimen JPEG ni PDF clínicos; elegí otra copia o solicitá aumentar el límite al administrador.");
                var original = await validation.ReadAsync(upload, options.Value.RequestMaxBytes - ClinicalFileOptions.MiB, ct);
                validation.Validate(original, type, upload.ContentType);
                var optimized = LosslessPngOptimizer.Optimize(original, options.Value.MaximumImagePixels * 9, ct);
                if (optimized.Length >= original.Length) return new(null, null, "No se obtuvo una reducción segura. Conservá el original, elegí otro archivo o solicitá ajustar el límite.");
                validation.Validate(optimized, type, type);
                ClinicalOperations.Audit(db, actor, "clinical.attachment.optimization-offered", recordId);
                await db.SaveChangesAsync(ct);
                return new(optimized, Path.GetFileNameWithoutExtension(name) + "-optimizada.png", null);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidDataException)
            { return new(null, null, "No fue posible optimizar sin riesgo de pérdida. El original no se modificó; elegí otra copia o solicitá ajustar el límite."); }
        }
        finally { gate.Semaphore.Release(); }
    }
}
