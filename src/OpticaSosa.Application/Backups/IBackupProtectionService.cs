namespace OpticaSosa.Application.Backups;

public sealed record BackupVolumeProtection(string VolumeId, int? ProtectionStatus, int? ConversionStatus,
    int? LockStatus, int RecoveryProtectors)
{
    public bool Protected => ProtectionStatus == 1 && ConversionStatus == 1 && LockStatus == 0 && RecoveryProtectors > 0;
}
public sealed record BackupProtectionReport(string Edition, bool ProviderAvailable, string ServiceState,
    string ServiceStartMode, int ServiceProcessId, IReadOnlyList<BackupVolumeProtection> Volumes, string? Alert);
public interface IBackupProtectionService
{
    Task<BackupProtectionReport> InspectAsync(CancellationToken ct = default);
}
