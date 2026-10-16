using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Clinical;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Clinical;

public sealed class ClinicalFilePolicyService(IDbContextFactory<OpticaDbContext> factory, ICurrentUser current,
    IOptions<ClinicalFileOptions> options, ClinicalFileGate gate) : IClinicalFilePolicyService
{
    public async Task<AttachmentPolicyDetails> GetAsync(CancellationToken ct = default)
    {
        if (current.Id is null || (!current.HasPermission(Permissions.ReadClinical) && !current.HasPermission(Permissions.ManageBusiness)))
            throw new UnauthorizedAccessException();
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.ClinicalFilePolicies.AsNoTracking().SingleOrDefaultAsync(ct) ?? new();
        return new(row.PhotoMiB, row.PdfMiB, row.Version, options.Value.RequestMaxMiB);
    }
    public async Task<ClinicalResult> SaveAsync(int photoMiB, int pdfMiB, Guid version, CancellationToken ct = default)
    {
        var actor = ClinicalOperations.Demand(current, Permissions.ManageBusiness);
        if (photoMiB < 1 || pdfMiB < 1 || Math.Max(photoMiB, pdfMiB) >= options.Value.RequestMaxMiB)
            return ClinicalOperations.Invalid($"Los tamaños deben ser positivos y menores que el techo técnico de {options.Value.RequestMaxMiB} MB por solicitud.");
        await gate.Semaphore.WaitAsync(ct);
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var row = await db.ClinicalFilePolicies.SingleOrDefaultAsync(ct);
            if ((row?.Version ?? Guid.Empty) != version) return ClinicalOperations.Conflict();
            if (row is null) { row = new ClinicalFilePolicy(); db.Add(row); }
            row.Update(photoMiB, pdfMiB);
            ClinicalOperations.Audit(db, actor, "clinical.file-policy.updated", null);
            return await ClinicalOperations.SaveAsync(db, Guid.Empty, ct);
        }
        finally { gate.Semaphore.Release(); }
    }
}
