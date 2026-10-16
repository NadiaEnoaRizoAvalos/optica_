using OpticaSosa.Application.Backups;
using OpticaSosa.Application.Security;

namespace OpticaSosa.Infrastructure.Backups;

public sealed class BackupService(BackupCoordinator coordinator, ICurrentUser current, ServerBackupFolders folders) : IBackupService
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
    public async Task<BackupAction> ConfigureAsync(BackupConfiguration input, CancellationToken ct = default)
    {
        var actor = Demand(Permissions.ManageBackups);
        var previous = await coordinator.OverviewAsync(ct);
        if (input.VolumeId is not null && (input.VolumeId != previous.Configuration.VolumeId ||
            input.RelativeFolder != previous.Configuration.RelativeFolder))
        {
            try { folders.Select(input.VolumeId, input.RelativeFolder); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            { return new(false, "Seleccioná una carpeta existente y permitida del disco conectado. No se guardó otro destino."); }
        }
        return await coordinator.ConfigureAsync(input, actor, ct);
    }
    public Task<BackupAction> RequestAsync(CancellationToken ct = default) => coordinator.RequestAsync(Demand(Permissions.ManageBackups), ct);
    public Task<BackupAction> BindDestinationAsync(Guid version, CancellationToken ct = default) =>
        coordinator.BindDestinationAsync(version, Demand(Permissions.ManageBackups), ct);
    public Task<BackupAction> VerifyAsync(Guid runId, CancellationToken ct = default) => coordinator.VerifyAsync(runId, Demand(Permissions.VerifyBackups), ct);
}
