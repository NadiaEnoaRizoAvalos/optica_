namespace OpticaSosa.Application.Backups;

public sealed record BackupConfiguration(bool Enabled, int EveryDays, string? LocalTime, string TimeZoneId,
    string? VolumeId, int KeepCopies, Guid Version, string RelativeFolder = "Respaldos", bool DestinationBound = false);
public sealed record BackupTarget(string VolumeId, string DisplayName, string FileSystem, string PhysicalDisks,
    long TotalBytes, long FreeBytes, bool Independent, bool Compatible, string Protection);
public sealed record BackupRunView(Guid Id, DateTime StartedUtc, DateTime? FinishedUtc, string Trigger,
    string Status, string? Error, int Files, int MissedPeriods);
public sealed record BackupDestinationView(BackupTarget? Disk, bool? Connected, string Message);
public sealed record BackupOverview(bool SchemaReady, BackupConfiguration Configuration,
    IReadOnlyList<BackupRunView> Runs, DateTime? NextUtc, DateTime? LastValidUtc, string? Alert, bool ExternalWritesAuthorized,
    DateTime? NextRetryUtc = null, BackupDestinationView? Destination = null,
    bool SignaturesRequired = false, bool SigningEnabled = false)
{
    public BackupRunView? LatestAttempt => Runs.FirstOrDefault();
}
public sealed record BackupAction(bool Succeeded, string Message);
public interface IBackupService
{
    Task<BackupOverview> OverviewAsync(CancellationToken ct = default);
    Task<IReadOnlyList<BackupTarget>> InspectTargetsAsync(CancellationToken ct = default);
    Task<BackupAction> ConfigureAsync(BackupConfiguration input, CancellationToken ct = default);
    Task<BackupAction> RequestAsync(CancellationToken ct = default);
    Task<BackupAction> BindDestinationAsync(Guid version, CancellationToken ct = default);
    Task<BackupAction> VerifyAsync(Guid runId, CancellationToken ct = default);
}
