using Microsoft.Extensions.Options;
using OpticaSosa.Infrastructure.Backups;

namespace OpticaSosa.Web.Backups;

public sealed class BackupWorker(BackupCoordinator backups, IOptions<BackupRuntimeOptions> options,
    TimeProvider clock, ILogger<BackupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.WorkerEnabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await backups.TickAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception)
            {
                // No exponer rutas, contenido de archivos ni credenciales en logs.
                logger.LogError("El programador de respaldos no pudo completar su revisión. Consultar el estado administrativo.");
            }
            try { await Task.Delay(TimeSpan.FromSeconds(15), clock, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }
}
