namespace OpticaSosa.Application.Clinical;

// Contrato de almacenamiento; los permisos y validaciones de contenido pertenecen al caso de uso.
// El identificador interno es la única entrada para construir rutas.
public interface IPrivateAttachmentStore
{
    Task WriteAsync(Guid id, byte[] bytes, CancellationToken ct = default);
    Task<byte[]?> ReadAsync(Guid id, CancellationToken ct = default);
    Task<Stream?> OpenReadAsync(Guid id, CancellationToken ct = default);
}
