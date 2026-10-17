using Microsoft.Extensions.Options;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Infrastructure.Backups;
using OpticaSosa.Infrastructure.Persistence;
using OpticaSosa.Infrastructure.Viewer;

namespace OpticaSosa.Viewer;

public sealed class ViewerPreparation(IConfiguration config, IWebHostEnvironment environment, ViewerSnapshot snapshot, BackupAuthenticity authenticity,
    IOptions<BackupRuntimeOptions> options, IStorageCapacity capacity) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        if (!config.GetValue<bool>("Viewer:FictitiousDataConfirmed"))
            throw new InvalidOperationException("Esta entrega sólo permite datos ficticios confirmados; uso real pendiente de seguridad/cifrado/custodias y autorización.");
        var source = config["Viewer:BackupDirectory"] ?? throw new InvalidOperationException("Seleccioná un respaldo firmado.");
        var workspace = config["Viewer:Workspace"] ?? throw new InvalidOperationException("Seleccioná una carpeta de trabajo nueva y privada.");
        var publicRoot = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        var paths = new StoragePaths(Path.GetFullPath(workspace), publicRoot);
        _ = new StoragePaths(Path.GetFullPath(source), publicRoot);
        var verifier = new BackupVerifier(paths, capacity, options, authenticity);
        await snapshot.OpenAsync(source, workspace, verifier, authenticity, ct);
    }
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask; // DI libera los handles al terminar las solicitudes.
}

internal sealed class ViewerCapacity : IStorageCapacity
{
    public long AvailableBytes => long.MaxValue;
}
