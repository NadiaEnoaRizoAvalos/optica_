using OpticaSosa.Application.Clinical;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Clinical;

public sealed class PrivateAttachmentStore(StoragePaths paths) : IPrivateAttachmentStore
{
    public async Task WriteAsync(Guid id, byte[] bytes, CancellationToken ct = default)
    {
        var final = FilePath(id);
        var temporary = Path.Combine(DirectoryPath(), Guid.NewGuid().ToString("N") + ".pending");
        try
        {
            // CreateNew y Move sin overwrite preservan un archivo ya publicado.
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await output.WriteAsync(bytes, ct);
                await output.FlushAsync(ct);
            }
            File.Move(temporary, final, overwrite: false);
        }
        finally
        {
            // Sólo el archivo transitorio GUID creado por esta llamada, nunca uno existente.
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public async Task<byte[]?> ReadAsync(Guid id, CancellationToken ct = default)
    {
        var file = FilePath(id);
        if (!File.Exists(file)) return null;
        RejectLink(file);
        // Límite defensivo de almacenamiento, independiente del límite funcional de la carga.
        if (new FileInfo(file).Length > 32 * 1024 * 1024) throw new IOException("Archivo privado fuera del límite de almacenamiento.");
        return await File.ReadAllBytesAsync(file, ct);
    }

    private string FilePath(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("Identificador de archivo inválido.");
        return Path.Combine(DirectoryPath(), id.ToString("N") + ".bin");
    }
    private string DirectoryPath()
    {
        RejectLink(paths.DataDirectory);
        RejectLink(paths.AttachmentsDirectory);
        var directory = Path.Combine(paths.AttachmentsDirectory, "clinical");
        Directory.CreateDirectory(directory);
        RejectLink(directory);
        return directory;
    }
    private static void RejectLink(string path)
    {
        if (File.Exists(path) || Directory.Exists(path))
            if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
                throw new IOException("El almacenamiento privado no admite enlaces.");
    }
}
