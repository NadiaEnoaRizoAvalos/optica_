using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Domain.Backups;

namespace OpticaSosa.Infrastructure.Backups;

public sealed partial class BackupCoordinator
{
    public async Task TickAsync(CancellationToken ct = default)
    {
        if (!runtime.Value.ExternalWritesAuthorized || !await ReadyAsync(ct) || !await gate.Semaphore.WaitAsync(0, ct)) return;
        try
        {
            // Exclusión entre instancias de la misma instalación; no depende de un hilo de Mutex.
            await using var processLock = TryLock();
            if (processLock is null) return;
            await using var db = await factory.CreateDbContextAsync(ct);
            var settings = await db.BackupSettings.SingleOrDefaultAsync(ct);
            if (settings?.VolumeId is null) return;
            var now = clock.GetUtcNow().UtcDateTime;
            foreach (var interrupted in await db.BackupRuns.Where(x => x.Status == "Running").ToListAsync(ct))
            {
                if (interrupted.DirectoryName is not null && interrupted.VolumeId is not null)
                {
                    try
                    {
                        var recovered = targets.Resolve(interrupted.VolumeId, settings.InstallationId, prepare: false,
                            interrupted.RelativeFolder, interrupted.BindingId);
                        var completedAt = await verifier.VerifyAsync(BackupFileSafety.Child(recovered.Directory, interrupted.DirectoryName), ct);
                        interrupted.Status = "Complete"; interrupted.FinishedUtc = completedAt;
                        if (interrupted.DueUtc is { } recoveredDue && settings.Enabled)
                        {
                            do { recoveredDue = BackupSchedule.Advance(recoveredDue, settings.EveryDays, settings.LocalTime!, settings.TimeZoneId); }
                            while (recoveredDue <= now);
                            settings.NextUtc = recoveredDue;
                        }
                        Audit(db, interrupted.ActorId, "backup.completed.recovered", interrupted.Id);
                        continue;
                    }
                    catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or SqliteException) { }
                }
                interrupted.Status = "Interrupted"; interrupted.FinishedUtc = now;
                interrupted.Error = "La ejecución fue interrumpida. Se verificará o reintentará sin inventar una copia histórica.";
                interrupted.RetryUtc = now;
            }
            await db.SaveChangesAsync(ct);
            var run = await db.BackupRuns.Where(x => x.Status == "Queued" || (x.Status == "Interrupted" && x.Trigger == "Manual"))
                .OrderBy(x => x.StartedUtc).FirstOrDefaultAsync(ct);
            if (run is { Status: "Interrupted" })
            {
                // Conservar intento interrumpido; crear otro ID/directorio, nunca sobrescribirlo.
                run.Status = "Failed";
                run = new BackupRun { StartedUtc = now, ActorId = run.ActorId, VolumeId = run.VolumeId,
                    RelativeFolder = run.RelativeFolder, BindingId = run.BindingId };
                db.Add(run);
            }
            if (run is null && settings.Enabled && settings.NextUtc is { } due && due <= now)
            {
                var previous = await db.BackupRuns.Where(x => x.DueUtc == due).OrderByDescending(x => x.StartedUtc).FirstOrDefaultAsync(ct);
                if (previous is { Status: "Failed" } && (previous.RetryUtc is null || previous.RetryUtc > now)) return;
                run = new BackupRun { StartedUtc = now, DueUtc = due, Trigger = "Scheduled", VolumeId = settings.VolumeId,
                    RelativeFolder = settings.RelativeFolder, BindingId = settings.BindingId };
                var next = BackupSchedule.Advance(due, settings.EveryDays, settings.LocalTime!, settings.TimeZoneId);
                while (next <= now)
                {
                    run.MissedPeriods++;
                    next = BackupSchedule.Advance(next, settings.EveryDays, settings.LocalTime!, settings.TimeZoneId);
                }
                if (run.MissedPeriods > 0 || previous?.Status == "Interrupted") run.Trigger = "Recovery";
                db.Add(run);
            }
            if (run is null) return;
            run.Status = "Running"; run.DirectoryName = run.Id.ToString("N");
            Audit(db, run.ActorId, "backup.started", run.Id); await db.SaveChangesAsync(ct);
            try
            {
                var target = targets.Resolve(run.VolumeId!, settings.InstallationId, prepare: true, run.RelativeFolder, run.BindingId);
                var required = writer.EstimateBytes();
                if (target.FreeBytes < required + runtime.Value.ReserveBytes) throw new IOException("Espacio insuficiente en el destino.");
                var copy = await writer.CreateAsync(target.Directory, ct, run.Id);
                run.DirectoryName = Path.GetFileName(copy.Directory);
                await verifier.VerifyAsync(copy.Directory, ct);
                run.Files = copy.Files; run.Status = "Complete"; run.FinishedUtc = clock.GetUtcNow().UtcDateTime;
                if (run.DueUtc is { } scheduled)
                {
                    settings.NextUtc = scheduled;
                    do { settings.NextUtc = BackupSchedule.Advance(settings.NextUtc.Value, settings.EveryDays, settings.LocalTime!, settings.TimeZoneId); }
                    while (settings.NextUtc <= run.FinishedUtc);
                    settings.Version = Guid.NewGuid();
                }
                Audit(db, run.ActorId, "backup.completed", run.Id); await db.SaveChangesAsync(ct);
                await RetainAsync(db, settings, target.Directory, run.VolumeId!, run.RelativeFolder, run.BindingId, ct);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or SqliteException or System.Text.Json.JsonException)
            {
                run.Status = "Failed"; run.FinishedUtc = clock.GetUtcNow().UtcDateTime;
                run.Error = "Falló el respaldo: revisá conexión, bloqueo, espacio, permisos e integridad del destino. No se usó otro disco ni se borraron copias anteriores.";
                if (run.DueUtc is { } failedDue && ex is not InvalidDataException)
                {
                    var attempts = await db.BackupRuns.CountAsync(x => x.DueUtc == failedDue, CancellationToken.None);
                    run.RetryUtc = run.FinishedUtc.Value.AddMinutes(attempts switch { 1 => 15, 2 => 30, _ => 60 });
                }
                Audit(db, run.ActorId, "backup.failed", run.Id); await db.SaveChangesAsync(CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                run.Status = "Interrupted"; run.FinishedUtc = clock.GetUtcNow().UtcDateTime;
                run.Error = "Copia interrumpida. No se considera válida hasta verificarla.";
                Audit(db, run.ActorId, "backup.interrupted", run.Id); await db.SaveChangesAsync(CancellationToken.None);
                throw;
            }
        }
        finally { gate.Semaphore.Release(); }
    }

    private FileStream? TryLock()
    {
        try { return new FileStream(Path.Combine(paths.DataDirectory, "backup-worker.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { return null; }
    }

    private async Task RetainAsync(Persistence.OpticaDbContext db, BackupSettings settings, string root, string volumeId,
        string relativeFolder, Guid? bindingId, CancellationToken ct)
    {
        var good = await db.BackupRuns.Where(x => x.Status == "Complete" && x.VolumeId == volumeId &&
                x.RelativeFolder == relativeFolder && x.BindingId == bindingId)
            .OrderByDescending(x => x.FinishedUtc).ToListAsync(ct);
        if (good.Count <= settings.KeepCopies) return;
        try
        {
            // Revalidar TODAS las que se conservarán antes de depurar cualquier copia previa.
            foreach (var keep in good.Take(settings.KeepCopies))
                await verifier.VerifyAsync(BackupFileSafety.Child(root, keep.DirectoryName!), ct);
            foreach (var old in good.Skip(settings.KeepCopies))
            {
                BackupFileSafety.DeleteOwnedCopy(root, old.DirectoryName!);
                old.Status = "Pruned";
                Audit(db, "system:backups", "backup.retention.pruned", old.Id);
                await db.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or SqliteException)
        {
            Audit(db, "system:backups", "backup.retention.failed");
            // El respaldo ya completo no se convierte en fallido por una limpieza fallida.
            good[0].Error = "La copia es válida, pero falló la retención. Se requiere revisar las copias anteriores.";
            await db.SaveChangesAsync(ct);
        }
    }
}
