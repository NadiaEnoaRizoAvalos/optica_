using Microsoft.Extensions.Options;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Infrastructure.Backups;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Viewer;

// A diferencia del almacén operativo, tampoco crea carpetas al consultar.
public sealed class ViewerAttachmentStore(StoragePaths paths, IOptions<ClinicalFileOptions> options) : IPrivateAttachmentStore
{
    public Task WriteAsync(Guid id, byte[] bytes, CancellationToken ct = default) =>
        throw new InvalidOperationException("El almacén del visor no permite escribir documentos.");

    public Task<Stream?> OpenReadAsync(Guid id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (id == Guid.Empty) throw new ArgumentException("Identificador de documento inválido.");
        var file = BackupFileSafety.Child(paths.DataDirectory, "attachments/clinical/" + id.ToString("N") + ".bin");
        return Task.FromResult<Stream?>(File.Exists(file)
            ? new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan)
            : null);
    }

    public async Task<byte[]?> ReadAsync(Guid id, CancellationToken ct = default)
    {
        await using var input = await OpenReadAsync(id, ct);
        if (input is null) return null;
        if (input.Length > options.Value.RequestMaxBytes) throw new IOException("Usá lectura por streaming para documentos extensos.");
        using var output = new MemoryStream(); await input.CopyToAsync(output, ct); return output.ToArray();
    }
}
