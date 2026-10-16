using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpticaSosa.Application.Backups;
using OpticaSosa.Domain.Backups;
using OpticaSosa.Infrastructure.Clinical;
using OpticaSosa.Infrastructure.Identity;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Backups;

public sealed partial class BackupCoordinator(IDbContextFactory<OpticaDbContext> factory, StoragePaths paths,
    IBackupTargets targets, ClinicalSnapshotWriter writer, BackupVerifier verifier, BackupExecutionGate gate,
    IOptions<BackupRuntimeOptions> runtime, TimeProvider clock)
{
    public async Task<bool> ReadyAsync(CancellationToken ct)
    {
        if (!File.Exists(paths.DatabaseFile)) return false;
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = paths.DatabaseFile, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('BackupSettings','BackupRuns')";
        if ((long)(await command.ExecuteScalarAsync(ct))! != 2) return false;
        command.CommandText = "SELECT (SELECT COUNT(*) FROM pragma_table_info('BackupSettings') WHERE name IN ('RelativeFolder','BindingId')) + (SELECT COUNT(*) FROM pragma_table_info('BackupRuns') WHERE name IN ('RelativeFolder','BindingId'))";
        return (long)(await command.ExecuteScalarAsync(ct))! == 4;
    }

    public async Task<BackupOverview> OverviewAsync(CancellationToken ct)
    {
        var defaults = new BackupSettings();
        if (!await ReadyAsync(ct)) return new(false, Map(defaults), [], null, null,
            "Respaldos nuevos no habilitados: falta revisar y aplicar la migración con autorización. La copia manual anterior sigue disponible.", runtime.Value.ExternalWritesAuthorized);
        await using var db = await factory.CreateDbContextAsync(ct);
        var settings = await db.BackupSettings.AsNoTracking().SingleOrDefaultAsync(ct) ?? defaults;
        var rows = await db.BackupRuns.AsNoTracking().OrderByDescending(x => x.StartedUtc).ThenBy(x => x.Id).Take(50).ToListAsync(ct);
        var valid = await db.BackupRuns.AsNoTracking().Where(x => x.Status == "Complete")
            .OrderByDescending(x => x.FinishedUtc).Select(x => x.FinishedUtc).FirstOrDefaultAsync(ct);
        var latest = rows.FirstOrDefault();
        var alert = latest?.Error;
        var destination = InspectConfiguredDestination(settings.VolumeId);
        if (settings.VolumeId is not null && destination.Connected != true) alert ??= destination.Message;
        if (settings.Enabled && settings.NextUtc < clock.GetUtcNow().UtcDateTime) alert ??= "La programación tiene una ejecución vencida o pendiente de recuperación.";
        if (!runtime.Value.ExternalWritesAuthorized) alert ??= "Escritura externa bloqueada hasta autorizar la instalación. No se tocará ningún disco externo.";
        else if (settings.VolumeId is not null)
        {
            try { targets.Resolve(settings.VolumeId, settings.InstallationId, prepare: false, settings.RelativeFolder, settings.BindingId); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            { alert = "El destino está desconectado, bloqueado, sin preparar o sin acceso. No se usará otro disco. " + alert; }
        }
        return new(true, Map(settings), rows.Select(x => new BackupRunView(x.Id, x.StartedUtc, x.FinishedUtc,
            x.Trigger, x.Status, x.Error, x.Files, x.MissedPeriods)).ToArray(), settings.Enabled ? settings.NextUtc : null,
            valid, alert, runtime.Value.ExternalWritesAuthorized, settings.Enabled ? latest?.RetryUtc : null, destination);
    }

    private BackupDestinationView InspectConfiguredDestination(string? volumeId)
    {
        if (volumeId is null) return new(null, null, "Sin disco configurado.");
        try
        {
            var matches = targets.Inspect().Where(x => x.VolumeId == volumeId).ToArray();
            if (matches.Length == 0) return new(null, false, "Disco configurado desconectado o no disponible. No se usará otro destino.");
            if (matches.Length != 1) return new(null, null, "Identidad del destino ambigua: no se puede confirmar la conexión segura.");
            var disk = matches[0];
            return new(disk, true, disk.Compatible && disk.Independent
                ? "Disco conectado. La vinculación, los permisos y la autorización de escritura se validan por separado."
                : "Disco conectado, pero no cumple la compatibilidad o independencia requerida.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return new(null, null, "No se pudo consultar la conexión del disco configurado. Revisá los permisos y reconectalo."); }
    }

    public IReadOnlyList<BackupTarget> InspectTargets() => targets.Inspect();

    public async Task<BackupAction> ConfigureAsync(BackupConfiguration input, string actor, CancellationToken ct)
    {
        if (!await ReadyAsync(ct)) return new(false, "La migración de respaldos no está aplicada. No se modificó la base.");
        if (input.EveryDays is < 1 or > 365 || input.KeepCopies is < 1 or > 3650)
            return new(false, "Frecuencia: entre 1 y 365 días. Conservación: entre 1 y 3650 copias.");
        string relativeFolder;
        try { relativeFolder = BackupFolder.Normalize(input.RelativeFolder); }
        catch (ArgumentException ex) { return new(false, ex.Message); }
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(input.TimeZoneId);
            if (!string.IsNullOrEmpty(input.LocalTime)) _ = BackupSchedule.ParseTime(input.LocalTime);
            if (input.Enabled && (string.IsNullOrEmpty(input.LocalTime) || string.IsNullOrEmpty(input.VolumeId)))
                return new(false, "Elegí horario y destino antes de activar.");
        }
        catch (Exception ex) when (ex is ArgumentException or TimeZoneNotFoundException or InvalidTimeZoneException)
        { return new(false, "Revisá horario y zona horaria."); }
        if (input.Enabled && input.VolumeId is not null && !targets.Inspect().Any(x => x.VolumeId == input.VolumeId && x.Compatible && x.Independent))
            return new(false, "No se pudo verificar el disco configurado. Reconectalo o elegí explícitamente otro; no se cambió el destino.");
        if (input.Enabled && !runtime.Value.ExternalWritesAuthorized) return new(false, "No se autorizó usar el disco real en esta instalación. La programación debe permanecer desactivada.");
        await gate.Semaphore.WaitAsync(ct);
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var settings = await db.BackupSettings.SingleOrDefaultAsync(ct);
            if (settings is not null && settings.Version != input.Version) return new(false, "La configuración cambió. Recargá antes de guardar.");
            if (settings is null) { settings = new(); db.Add(settings); }
            var destinationChanged = settings.VolumeId != input.VolumeId || settings.RelativeFolder != relativeFolder;
            if (input.Enabled && (destinationChanged || settings.BindingId is null))
                return new(false, "Guardá el destino con programación desactivada, vinculá la carpeta privada y después activá el horario.");
            if (input.Enabled)
            {
                try { targets.Resolve(input.VolumeId!, settings.InstallationId, false, relativeFolder, settings.BindingId); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { return new(false, "El destino vinculado no está accesible o su marcador cambió. No se activa la programación."); }
            }
            var scheduleChanged = settings.Enabled != input.Enabled || settings.LocalTime != input.LocalTime ||
                settings.TimeZoneId != input.TimeZoneId || settings.EveryDays != input.EveryDays;
            settings.Enabled = input.Enabled; settings.EveryDays = input.EveryDays;
            settings.LocalTime = string.IsNullOrEmpty(input.LocalTime) ? null : input.LocalTime;
            settings.TimeZoneId = input.TimeZoneId; settings.VolumeId = input.VolumeId;
            settings.RelativeFolder = relativeFolder;
            if (destinationChanged) settings.BindingId = null;
            settings.KeepCopies = input.KeepCopies; settings.Version = Guid.NewGuid();
            if (!settings.Enabled) settings.NextUtc = null;
            else if (scheduleChanged || settings.NextUtc is null)
                settings.NextUtc = BackupSchedule.First(clock.GetUtcNow().UtcDateTime, settings.LocalTime!, settings.TimeZoneId);
            Audit(db, actor, "backup.settings.changed");
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { return new(false, "La configuración cambió. Recargá antes de guardar."); }
            await transaction.CommitAsync(ct);
            return new(true, "Configuración guardada. El disco no se modificó.");
        }
        finally { gate.Semaphore.Release(); }
    }

    public async Task<BackupAction> RequestAsync(string actor, CancellationToken ct)
    {
        if (!runtime.Value.ExternalWritesAuthorized) return new(false, "Escritura externa aún no autorizada. La copia manual local anterior sigue disponible.");
        if (!await ReadyAsync(ct)) return new(false, "Primero debe aprobarse y aplicarse la migración.");
        await gate.Semaphore.WaitAsync(ct);
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var settings = await db.BackupSettings.SingleOrDefaultAsync(ct);
            if (settings?.VolumeId is null) return new(false, "Seleccioná explícitamente un disco de destino.");
            if (settings.BindingId is null) return new(false, "Primero vinculá la carpeta privada del destino con autorización.");
            if (await db.BackupRuns.AnyAsync(x => x.Status == "Queued" || x.Status == "Running", ct)) return new(false, "Ya existe una copia pendiente o en curso.");
            db.Add(new BackupRun { StartedUtc = clock.GetUtcNow().UtcDateTime, ActorId = actor, VolumeId = settings.VolumeId,
                RelativeFolder = settings.RelativeFolder, BindingId = settings.BindingId });
            Audit(db, actor, "backup.queued"); await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(true, "Copia encolada. Continuará aunque cierres el navegador mientras el servidor siga activo.");
        }
        finally { gate.Semaphore.Release(); }
    }

    public async Task<BackupAction> VerifyAsync(Guid id, string actor, CancellationToken ct)
    {
        if (!runtime.Value.ExternalWritesAuthorized || !await ReadyAsync(ct)) return new(false, "No se autorizó operar con el destino o falta la migración.");
        await gate.Semaphore.WaitAsync(ct);
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var settings = await db.BackupSettings.SingleOrDefaultAsync(ct);
            var run = await db.BackupRuns.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (settings is null || run?.Status != "Complete" || run.VolumeId is null || run.DirectoryName is null)
                return new(false, "No se encontró una copia completa para verificar.");
            try
            {
                var target = targets.Resolve(run.VolumeId, settings.InstallationId, prepare: false, run.RelativeFolder, run.BindingId);
                var directory = BackupFileSafety.Child(target.Directory, run.DirectoryName);
                try { await verifier.VerifyAsync(directory, ct); }
                catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or SqliteException)
                {
                    run.Status = "Corrupt"; run.Error = "La copia dejó de superar la verificación de integridad. No se considera recuperable.";
                    Audit(db, actor, "backup.integrity.failed", id); await db.SaveChangesAsync(ct);
                    return new(false, run.Error);
                }
                await verifier.RestoreIsolatedAsync(directory, ct);
                Audit(db, actor, "backup.restore-test.verified", id); await db.SaveChangesAsync(ct);
                return new(true, "Copia restaurada y verificada en directorio privado aislado. No se inició un servidor ni se modificó la base activa. Claves/cifrado aún requieren verificación operativa.");
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or SqliteException)
            {
                run.Error = "No se completó el ensayo de recuperación. Revisar conexión, permisos y espacio del entorno aislado.";
                Audit(db, actor, "backup.restore-test.failed", id); await db.SaveChangesAsync(ct);
                return new(false, "No se pudo verificar la recuperación. Revisá destino, espacio e integridad; la base activa no se reemplazó.");
            }
        }
        finally { gate.Semaphore.Release(); }
    }

    private static BackupConfiguration Map(BackupSettings s) => new(s.Enabled, s.EveryDays, s.LocalTime, s.TimeZoneId,
        s.VolumeId, s.KeepCopies, s.Version, s.RelativeFolder, s.BindingId is not null);
    private static void Audit(OpticaDbContext db, string? actor, string action, Guid? id = null) =>
        db.AuditEntries.Add(new AuditEntry { ActorId = actor ?? "system:backups", Action = action, TargetId = id?.ToString() });
}
