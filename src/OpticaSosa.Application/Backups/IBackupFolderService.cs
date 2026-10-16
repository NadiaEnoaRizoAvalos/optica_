namespace OpticaSosa.Application.Backups;

public sealed record BackupFolderEntry(string Name, string RelativeFolder);
public sealed record BackupFolderSelection(string VolumeId, string RelativeFolder, string FullPath);
public sealed record BackupFolderPage(BackupTarget Disk, string RelativeFolder, string FullPath,
    IReadOnlyList<BackupFolderEntry> Items, int Page, bool HasMore, bool CanCreate);
public interface IBackupFolderService
{
    Task<BackupFolderPage> BrowseAsync(string volumeId, string relativeFolder = "", int page = 1, CancellationToken ct = default);
    Task<BackupFolderSelection> SelectAsync(string volumeId, string relativeFolder, CancellationToken ct = default);
    Task<BackupAction> CreateAsync(string volumeId, string parent, string name, bool confirmed, CancellationToken ct = default);
}
