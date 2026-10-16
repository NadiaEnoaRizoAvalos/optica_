using System.Runtime.InteropServices;
using OpticaSosa.Application.Backups;
using OpticaSosa.Domain.Backups;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Backups;

public sealed class ServerBackupFolders(IBackupTargets targets, StoragePaths paths)
{
    private static readonly HashSet<string> ProtectedNames = new(StringComparer.OrdinalIgnoreCase)
    { "Windows", "Users", "Program Files", "Program Files (x86)", "ProgramData", "Recovery", "EFI", "Boot", "MSOCache", "Config.Msi" };

    public BackupFolderPage Browse(string volumeId, string relative, int page, bool allowCreate, CancellationToken ct)
    {
        if (page is < 1 or > 100000) throw new ArgumentException("La página de carpetas no es válida.");
        var volume = targets.ResolveVolume(volumeId);
        var normalized = Normalize(relative, allowRoot: true);
        var directory = DirectoryPath(volume.Root, normalized);
        using var lease = new WindowsDirectoryLease(volume.Root, normalized);
        var items = new List<BackupFolderEntry>();
        foreach (var child in Directory.EnumerateDirectories(directory))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var name = Path.GetFileName(child);
                var candidate = normalized.Length == 0 ? name : normalized + "/" + name;
                DirectoryPath(volume.Root, Normalize(candidate, false));
                items.Add(new(name, candidate));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
        }
        var batch = items.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Skip((page - 1) * 50).Take(51).ToArray();
        return new(volume.Disk, normalized, directory, batch.Take(50).ToArray(), page, batch.Length > 50,
            allowCreate && CanCreate(directory, normalized));
    }

    public BackupFolderSelection Select(string volumeId, string relative)
    {
        var volume = targets.ResolveVolume(volumeId);
        var normalized = Normalize(relative, false);
        using var lease = new WindowsDirectoryLease(volume.Root, normalized);
        return new(volumeId, normalized, DirectoryPath(volume.Root, normalized));
    }

    public void Create(string volumeId, string parent, string name)
    {
        var volume = targets.ResolveVolume(volumeId);
        var normalized = Normalize(parent, false);
        var segment = Normalize(name, false);
        if (segment.Contains('/') || Forbidden(segment)) throw new ArgumentException("Indicá sólo un nombre de carpeta permitido.");
        var newRelative = Normalize(normalized + "/" + segment, false);
        using var lease = new WindowsDirectoryLease(volume.Root, normalized);
        var directory = DirectoryPath(volume.Root, normalized);
        if (!CanCreate(directory, normalized)) throw new IOException("La carpeta padre debe tener permisos privados autorizados. No se cambiarán sus permisos.");
        var child = BackupFileSafety.Child(directory, segment);
        if (!OperatingSystem.IsWindows()) throw new IOException("Se requiere Windows.");
        if (!CreateDirectory(child, IntPtr.Zero)) throw new IOException("No se creó la carpeta: ya existe o faltan permisos/espacio. No se sobrescribió ningún elemento.");
        using var childLease = new WindowsDirectoryLease(volume.Root, newRelative);
        WindowsBackupTargets.RequirePrivateAcl(child);
    }

    private string DirectoryPath(string root, string relative)
    {
        BackupFileSafety.RejectLinks(root);
        var path = relative.Length == 0 ? Path.GetFullPath(root) : BackupFileSafety.Child(root, relative);
        var current = root;
        foreach (var segment in relative.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Forbidden(segment))
                throw new IOException("Esa carpeta no está permitida para explorar respaldos.");
            current = Path.Combine(current, segment);
            var attributes = File.GetAttributes(current);
            if ((attributes & (FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint)) != 0)
                throw new IOException("No se exploran carpetas ocultas, del sistema ni enlazadas.");
            if (File.Exists(Path.Combine(current, "optica.db")) || File.Exists(Path.Combine(current, "complete.json")))
                throw new IOException("No se explora el contenido de bases o copias clínicas.");
        }
        var data = Path.GetRelativePath(paths.DataDirectory, path);
        if (data == "." || (!Path.IsPathRooted(data) && data != ".." && !data.StartsWith(".." + Path.DirectorySeparatorChar)))
            throw new IOException("El almacenamiento activo no es un destino permitido.");
        if (!Directory.Exists(path)) throw new IOException("La carpeta seleccionada no existe o no está disponible.");
        return path;
    }

    private static string Normalize(string value, bool allowRoot) => allowRoot && value.Length == 0 ? "" : BackupFolder.Normalize(value);
    private static bool Forbidden(string name) => name.StartsWith('.') || name.StartsWith('$') || ProtectedNames.Contains(name);
    private static bool CanCreate(string directory, string relative)
    {
        if (relative.Length == 0 || !OperatingSystem.IsWindows()) return false;
        try { WindowsBackupTargets.RequirePrivateAcl(directory); return true; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateDirectory(string path, IntPtr security);
}
