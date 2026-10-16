using OpticaSosa.Application.Backups;
using OpticaSosa.Application.Security;

namespace OpticaSosa.Infrastructure.Backups;

public sealed class BackupService(BackupCoordinator coordinator, ICurrentUser current) : IBackupService
{
    private string Demand(string permission)
    {
        if (current.Id is null || !current.HasPermission(permission)) throw new UnauthorizedAccessException();
        return current.Id;
    }
    public Task<BackupOverview> OverviewAsync(CancellationToken ct = default)
    { Demand(Permissions.ManageBackups); return coordinator.OverviewAsync(ct); }
    public Task<IReadOnlyList<BackupTarget>> InspectTargetsAsync(CancellationToken ct = default)
    { Demand(Permissions.ManageBackups); ct.ThrowIfCancellationRequested(); return Task.FromResult(coordinator.InspectTargets()); }
    public Task<BackupAction> ConfigureAsync(BackupConfiguration input, CancellationToken ct = default) =>
        coordinator.ConfigureAsync(input, Demand(Permissions.ManageBackups), ct);
    public Task<BackupAction> RequestAsync(CancellationToken ct = default) => coordinator.RequestAsync(Demand(Permissions.ManageBackups), ct);
    public Task<BackupAction> VerifyAsync(Guid runId, CancellationToken ct = default) => coordinator.VerifyAsync(runId, Demand(Permissions.VerifyBackups), ct);
}
