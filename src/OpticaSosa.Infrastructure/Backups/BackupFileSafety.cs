namespace OpticaSosa.Infrastructure.Backups;

public static class BackupFileSafety
{
    public static void RejectLinks(string path)
    {
        for (var entry = new DirectoryInfo(Path.GetFullPath(path)); entry is not null; entry = entry.Parent)
            if (entry.Exists && entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new IOException("La ruta de respaldo no admite enlaces ni uniones.");
    }

    public static string Child(string root, string relative)
    {
        if (Path.IsPathRooted(relative)) throw new IOException("Ruta de copia inválida.");
        var full = Path.GetFullPath(Path.Combine(root, relative));
        var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new IOException("Ruta fuera de la copia.");
        RejectLinks(full);
        if (File.Exists(full) && File.GetAttributes(full).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Archivo enlazado no permitido.");
        return full;
    }

    public static void DeleteOwnedCopy(string root, string name)
    {
        if (!Guid.TryParseExact(name, "N", out _)) throw new IOException("Identificador de copia inválido.");
        var directory = Child(root, name);
        RejectTree(directory);
        Directory.Delete(directory, recursive: true);
    }

    private static void RejectTree(string root)
    {
        RejectLinks(root);
        foreach (var entry in Directory.EnumerateFileSystemEntries(root))
        {
            if (File.GetAttributes(entry).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("No se elimina una copia con enlaces.");
            if (Directory.Exists(entry)) RejectTree(entry);
        }
    }
}
