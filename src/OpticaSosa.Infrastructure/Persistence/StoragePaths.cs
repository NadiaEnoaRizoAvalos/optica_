namespace OpticaSosa.Infrastructure.Persistence;

public sealed class StoragePaths
{
    public string DataDirectory { get; }
    public string DatabaseFile => Path.Combine(DataDirectory, "optica.db");
    public string AttachmentsDirectory => Path.Combine(DataDirectory, "attachments");

    public StoragePaths(string dataDirectory, string webRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(webRoot);

        if (!Path.IsPathFullyQualified(dataDirectory) || !Path.IsPathFullyQualified(webRoot))
            throw new ArgumentException("Las rutas de almacenamiento deben ser absolutas.");

        DataDirectory = Path.GetFullPath(dataDirectory);
        var publicDirectory = Path.GetFullPath(webRoot);
        if (IsWithin(DataDirectory, publicDirectory) || IsWithin(publicDirectory, DataDirectory))
            throw new ArgumentException("El almacenamiento privado debe estar separado de la carpeta pública.");

        if (OperatingSystem.IsWindows() &&
            (DataDirectory.StartsWith(@"\\", StringComparison.Ordinal) ||
             new DriveInfo(Path.GetPathRoot(DataDirectory)!).DriveType == DriveType.Network))
            throw new ArgumentException("La base activa requiere un disco local.");

        RejectLinkedAncestors(DataDirectory);
    }

    private static bool IsWithin(string candidate, string parent)
    {
        var relative = Path.GetRelativePath(parent, candidate);
        return relative == "." ||
            (!Path.IsPathRooted(relative) && relative != ".." &&
             !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal));
    }

    private static void RejectLinkedAncestors(string directory)
    {
        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
        {
            if (current.Exists && current.Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new ArgumentException("La ruta de datos no puede atravesar enlaces o uniones de carpetas.");
        }
    }
}
