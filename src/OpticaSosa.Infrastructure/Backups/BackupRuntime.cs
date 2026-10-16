namespace OpticaSosa.Infrastructure.Backups;

public sealed class BackupRuntimeOptions
{
    // Puerta de autorización de instalación: no editable desde el navegador.
    public bool ExternalWritesAuthorized { get; set; }
    public bool FolderCreationAuthorized { get; set; }
    public bool WorkerEnabled { get; set; } = true;
    public long ReserveBytes { get; set; } = 512L * 1024 * 1024;
}

public sealed class BackupExecutionGate : IDisposable
{
    public SemaphoreSlim Semaphore { get; } = new(1, 1);
    public void Dispose() => Semaphore.Dispose();
}
