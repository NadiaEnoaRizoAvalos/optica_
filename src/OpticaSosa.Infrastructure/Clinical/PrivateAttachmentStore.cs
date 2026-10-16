using OpticaSosa.Application.Clinical;
using OpticaSosa.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace OpticaSosa.Infrastructure.Clinical;

public sealed class PrivateAttachmentStore(StoragePaths paths, IOptions<ClinicalFileOptions> options) : IPrivateAttachmentStore
{
    public async Task WriteAsync(Guid id, byte[] bytes, CancellationToken ct = default)
    {
        var final = FilePath(id);
        var temporary = Path.Combine(DirectoryPath(), id.ToString("N") + "-" + Guid.NewGuid().ToString("N") + ".pending");
        try
        {
            // CreateNew y Move sin overwrite preservan un archivo ya publicado.
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await output.WriteAsync(bytes, ct);
                await output.FlushAsync(ct);
            }
            ct.ThrowIfCancellationRequested();
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
        await using var input = await OpenReadAsync(id, ct);
        if (input is null) return null;
        if (input.Length > options.Value.RequestMaxBytes) throw new IOException("Archivo fuera del límite de lectura en memoria; usar descarga por streaming.");
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }

    public Task<Stream?> OpenReadAsync(Guid id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var file = FilePath(id);
        if (!File.Exists(file)) return Task.FromResult<Stream?>(null);
        RejectLink(file);
        return Task.FromResult<Stream?>(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan));
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
