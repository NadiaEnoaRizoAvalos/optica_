using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;
using OpticaSosa.Application.Backups;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Backups;

public sealed record ResolvedBackupTarget(string Directory, long FreeBytes);
public sealed record ResolvedBackupVolume(string Root, BackupTarget Disk);
public interface IBackupTargets
{
    IReadOnlyList<BackupTarget> Inspect();
    ResolvedBackupVolume ResolveVolume(string volumeId);
    ResolvedBackupTarget Resolve(string volumeId, Guid installationId, bool prepare,
        string relativeFolder = "Respaldos", Guid? bindingId = null);
    Guid Bind(string volumeId, Guid installationId, string relativeFolder);
}

public sealed class WindowsBackupTargets(StoragePaths paths) : IBackupTargets
{
    public IReadOnlyList<BackupTarget> Inspect()
    {
        if (!OperatingSystem.IsWindows()) return [];
        var sourceDisks = Disks(Volume(Path.GetPathRoot(paths.DataDirectory)!));
        var result = new List<BackupTarget>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady || drive.DriveType is not (DriveType.Fixed or DriveType.Removable)) continue;
                var id = Volume(drive.RootDirectory.FullName);
                var disks = Disks(id);
                var independent = sourceDisks.Length > 0 && disks.Length > 0 && !sourceDisks.Intersect(disks).Any();
                result.Add(new(id, $"{drive.Name} {drive.VolumeLabel}", drive.DriveFormat,
                    string.Join(", ", disks.Select(x => $"Disco físico {x}")), drive.TotalSize, drive.AvailableFreeSpace,
                    independent, independent && drive.DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase),
                    "Cifrado no verificado. Antes de escribir se requieren autorización y carpeta con ACL privadas."));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* No ofrecer un destino no verificable. */ }
        }
        return result;
    }

    public ResolvedBackupTarget Resolve(string volumeId, Guid installationId, bool prepare,
        string relativeFolder = "Respaldos", Guid? bindingId = null)
    {
        var parent = ResolveParent(volumeId, relativeFolder);
        BackupTargetBinding.Validate(parent.Directory, volumeId, installationId, bindingId);
        var root = BackupFileSafety.Child(parent.Directory, installationId.ToString("N"));
        if (prepare) Directory.CreateDirectory(root);
        if (OperatingSystem.IsWindows() && Directory.Exists(root)) RequirePrivateAcl(root);
        return new(root, parent.FreeBytes);
    }

    public Guid Bind(string volumeId, Guid installationId, string relativeFolder)
    {
        var parent = ResolveParent(volumeId, relativeFolder);
        return BackupTargetBinding.Bind(parent.Directory, volumeId, installationId);
    }

    private ResolvedBackupTarget ResolveParent(string volumeId, string relativeFolder)
    {
        var volume = ResolveVolume(volumeId);
        var parent = BackupFileSafety.Child(volume.Root, OpticaSosa.Domain.Backups.BackupFolder.Normalize(relativeFolder));
        if (!Directory.Exists(parent)) throw new IOException("La carpeta privada de respaldos aún no fue preparada con autorización.");
        if (!OperatingSystem.IsWindows()) throw new IOException("Destino Windows no disponible.");
        RequirePrivateAcl(parent);
        return new(parent, volume.Disk.FreeBytes);
    }

    public ResolvedBackupVolume ResolveVolume(string volumeId)
    {
        if (!OperatingSystem.IsWindows()) throw new IOException("Destino Windows no disponible.");
        var matches = Inspect().Where(x => x.VolumeId == volumeId).ToArray();
        if (matches.Length != 1) throw new IOException("El disco configurado está desconectado, bloqueado o su identidad es ambigua. No se usará otro destino.");
        var target = matches[0];
        if (!target.Compatible || !target.Independent) throw new IOException("Se requiere NTFS y un disco físico independiente del origen.");
        // Se resuelve la letra actual a partir del GUID, no se persiste una letra como identidad.
        var drive = DriveInfo.GetDrives().FirstOrDefault(x => x.IsReady && Volume(x.Name) == volumeId)
            ?? throw new IOException("El disco se desconectó durante la consulta. No se usará otro destino.");
        return new(drive.RootDirectory.FullName, target);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    internal static void RequirePrivateAcl(string directory)
    {
        var allowed = new[] { WindowsIdentity.GetCurrent().User!.Value, "S-1-5-18", "S-1-5-32-544" };
        var acl = new DirectoryInfo(directory).GetAccessControl();
        foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            if (rule.AccessControlType == AccessControlType.Allow && !allowed.Contains(rule.IdentityReference.Value))
                throw new IOException("La carpeta permite acceso a otras identidades Windows. Revisar ACL con autorización antes de usarla.");
    }

    private static string Volume(string mount)
    {
        var buffer = new StringBuilder(512);
        if (!GetVolumeNameForVolumeMountPoint(mount, buffer, buffer.Capacity)) throw new IOException("No se pudo identificar el volumen.");
        return buffer.ToString();
    }

    private static int[] Disks(string volume)
    {
        using var handle = CreateFile(volume.TrimEnd('\\'), 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        var data = new byte[4096];
        if (handle.IsInvalid || !DeviceIoControl(handle, 0x00560000, IntPtr.Zero, 0, data, data.Length, out var size, IntPtr.Zero))
            throw new IOException("No se pudo verificar la unidad física; no se autoriza el destino.");
        var count = BitConverter.ToInt32(data, 0);
        if (count < 1 || count > 100 || size < 8 + 24 * count) throw new IOException("Identidad física no verificable.");
        return Enumerable.Range(0, count).Select(i => BitConverter.ToInt32(data, 8 + i * 24)).Distinct().ToArray();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeNameForVolumeMountPoint(string mount, StringBuilder name, int length);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle handle, uint code, IntPtr input, int inputSize,
        byte[] output, int outputSize, out int returned, IntPtr overlapped);
}
