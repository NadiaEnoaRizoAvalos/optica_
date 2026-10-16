using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Security;
using OpticaSosa.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace OpticaSosa.Infrastructure.Clinical;

// Mantiene el contrato manual. El motor de copia se comparte con la programación.
public sealed class ClinicalSnapshotService(ClinicalSnapshotWriter writer, ICurrentUser current,
    StoragePaths paths, IDbContextFactory<OpticaDbContext> factory) : IClinicalSnapshotService
{
    public async Task<ClinicalSnapshot> CreateAsync(CancellationToken ct = default)
    {
        var actor = ClinicalOperations.Demand(current, Permissions.ManageBusiness);
        await AuditAsync(actor, "clinical.snapshot.started", ct);
        try
        {
            var result = await writer.CreateAsync(Path.Combine(paths.DataDirectory, "clinical-backups"), ct);
            await AuditAsync(actor, "clinical.snapshot.completed", ct);
            return result;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or OperationCanceledException)
        {
            await AuditAsync(actor, "clinical.snapshot.failed", CancellationToken.None);
            throw;
        }
    }
    private async Task AuditAsync(string actor, string action, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        ClinicalOperations.Audit(db, actor, action, null);
        await db.SaveChangesAsync(ct);
    }
}
