using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OpticaSosa.Application.Viewer;
using OpticaSosa.Infrastructure.Identity;

namespace OpticaSosa.Infrastructure.Viewer;

public sealed class ViewerReadGuard(IViewerAudit audit) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) =>
        throw new InvalidOperationException("El visor sólo permite consultas asíncronas y auditoría independiente.");

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var changes = eventData.Context!.ChangeTracker.Entries()
            .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray();
        if (changes.Any(x => x.State != EntityState.Added || x.Entity is not AuditEntry))
            throw new InvalidOperationException("No se puede modificar información desde el visor de emergencia.");
        foreach (var entry in changes)
        {
            var item = (AuditEntry)entry.Entity;
            await audit.RecordAsync(item.Action, item.ActorId, item.TargetId, cancellationToken);
            entry.State = EntityState.Detached;
        }
        return InterceptionResult<int>.SuppressWithResult(changes.Length);
    }
}
