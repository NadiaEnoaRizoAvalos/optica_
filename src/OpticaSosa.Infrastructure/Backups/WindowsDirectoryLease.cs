using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace OpticaSosa.Infrastructure.Backups;

// Mantiene abiertos los ancestros sin compartir escritura/eliminación durante cada operación.
internal sealed class WindowsDirectoryLease : IDisposable
{
    private readonly List<SafeFileHandle> handles = [];
    public WindowsDirectoryLease(string root, string relative)
    {
        try
        {
            var path = root;
            Open(path);
            foreach (var segment in relative.Split('/', StringSplitOptions.RemoveEmptyEntries))
            { path = Path.Combine(path, segment); Open(path); }
        }
        catch { Dispose(); throw; }
    }

    private void Open(string path)
    {
        if (!OperatingSystem.IsWindows()) throw new IOException("El explorador requiere un servidor Windows.");
        var handle = CreateFile(Path.TrimEndingDirectorySeparator(path),
            0x80, 1, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
        if (handle.IsInvalid) { handle.Dispose(); throw new IOException("La carpeta no está disponible, está en uso o no permite acceso seguro."); }
        handles.Add(handle);
        var attributes = File.GetAttributes(path);
        if (!attributes.HasFlag(FileAttributes.Directory) || attributes.HasFlag(FileAttributes.ReparsePoint))
            throw new IOException("No se admiten archivos, enlaces ni uniones como carpetas de destino.");
    }
    public void Dispose() { foreach (var handle in handles.AsEnumerable().Reverse()) handle.Dispose(); }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security,
        uint creation, uint flags, IntPtr template);
}
