using OpticaSosa.Application.Clinical;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Clinical;

// Serializa tareas de archivos para acotar memoria y coordinar cargas con respaldos.
public sealed class ClinicalFileGate : IDisposable
{
    public SemaphoreSlim Semaphore { get; } = new(1, 1);
    public void Dispose() => Semaphore.Dispose();
}

public sealed class StorageCapacity(StoragePaths paths) : IStorageCapacity
{
    public long AvailableBytes => new DriveInfo(Path.GetPathRoot(paths.DataDirectory)!).AvailableFreeSpace;
}
