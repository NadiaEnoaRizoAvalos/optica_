using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Backups;

namespace OpticaSosa.Infrastructure.Backups;

public sealed partial class BackupCoordinator
{
    public async Task<BackupAction> BindDestinationAsync(Guid version, string actor, CancellationToken ct)
    {
        if (!runtime.Value.ExternalWritesAuthorized || !await ReadyAsync(ct))
            return new(false, "La vinculación requiere autorización de escritura y migración revisada. No se modificó el destino.");
        await gate.Semaphore.WaitAsync(ct);
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var settings = await db.BackupSettings.SingleOrDefaultAsync(ct);
            if (settings?.VolumeId is null || settings.Version != version)
                return new(false, "Guardá y revisá la configuración actual antes de vincular.");
            if (settings.Enabled) return new(false, "Desactivá la programación antes de vincular un destino.");
            try
            {
                settings.BindingId = targets.Bind(settings.VolumeId, settings.InstallationId, settings.RelativeFolder);
                settings.Version = Guid.NewGuid();
                // El I/O externo ocurre antes de la transacción implícita de SaveChanges,
                // para no mantener un bloqueo de escritura SQLite durante consultas al USB.
                Audit(db, actor, "backup.destination.bound"); await db.SaveChangesAsync(ct);
                return new(true, "Carpeta vinculada mediante un marcador privado de esta instalación. La programación continúa desactivada.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { return new(false, "No se pudo vincular: revisá identidad, carpeta existente y permisos privados. No se cambió otra carpeta ni se reemplazó un marcador."); }
            catch (DbUpdateConcurrencyException)
            { return new(false, "La configuración cambió durante la vinculación. Recargá y verificá el destino antes de continuar."); }
        }
        finally { gate.Semaphore.Release(); }
    }
}
