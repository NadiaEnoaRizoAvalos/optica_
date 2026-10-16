using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Security;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Identity;

public sealed class SecurityAudit(IDbContextFactory<OpticaDbContext> factory) : ISecurityAudit
{
    public async Task RecordAsync(string action, string? actorId, string? targetId = null, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        db.AuditEntries.Add(new AuditEntry { Action = action, ActorId = actorId, TargetId = targetId });
        await db.SaveChangesAsync(cancellationToken);
    }
}
