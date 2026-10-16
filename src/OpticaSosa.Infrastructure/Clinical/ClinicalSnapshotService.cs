using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Security;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Clinical;

// Copia manual para incluir documentos en fase 4. Programación, cifrado, retención
// y restauración operativa completa se mantienen en fase 7.
public sealed class ClinicalSnapshotService(IDbContextFactory<OpticaDbContext> factory, ICurrentUser current,
    StoragePaths paths, IStorageCapacity capacity, IOptions<ClinicalFileOptions> options, ClinicalFileGate gate) : IClinicalSnapshotService
{
    public async Task<ClinicalSnapshot> CreateAsync(CancellationToken ct = default)
    {
        var actor = ClinicalOperations.Demand(current, Permissions.ManageBusiness);
        await gate.Semaphore.WaitAsync(ct);
        try
        {
            var files = PrivateFiles(paths.AttachmentsDirectory).ToArray();
            var needed = files.Sum(x => new FileInfo(x).Length) + new FileInfo(paths.DatabaseFile).Length;
            if (capacity.AvailableBytes < needed + options.Value.ReserveDiskMiB * ClinicalFileOptions.MiB)
                throw new IOException("No hay espacio para una copia completa manteniendo la reserva de seguridad.");
            var backupRoot = Path.Combine(paths.DataDirectory, "clinical-backups");
            Directory.CreateDirectory(backupRoot);
            if (File.GetAttributes(backupRoot).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("El destino privado no admite enlaces.");
            var destination = Path.Combine(backupRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(destination);
            await using var db = await factory.CreateDbContextAsync(ct);
            ClinicalOperations.Audit(db, actor, "clinical.snapshot.started", null);
            await db.SaveChangesAsync(ct);
            await db.Database.OpenConnectionAsync(ct);
            var databaseFile = Path.Combine(destination, "optica.db");
            await using (var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databaseFile, Pooling = false }.ToString()))
            {
                await target.OpenAsync(ct);
                ((SqliteConnection)db.Database.GetDbConnection()).BackupDatabase(target);
            }
            var manifest = new Dictionary<string, string>();
            foreach (var source in files)
            {
                ct.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(paths.AttachmentsDirectory, source);
                var copy = Path.Combine(destination, "attachments", relative);
                Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
                await using (var output = new FileStream(copy, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
                    await input.CopyToAsync(output, ct);
                input.Position = 0;
                var originalHash = Convert.ToHexString(await SHA256.HashDataAsync(input, ct));
                await using var copied = File.OpenRead(copy);
                if (originalHash != Convert.ToHexString(await SHA256.HashDataAsync(copied, ct))) throw new IOException("La copia de un documento no coincide.");
                manifest[relative] = originalHash;
            }
            await VerifyDatabaseAsync(databaseFile, destination, ct);
            await using (var database = File.OpenRead(databaseFile))
                manifest["optica.db"] = Convert.ToHexString(await SHA256.HashDataAsync(database, ct));
            // Sin este manifiesto final la carpeta es incompleta y no debe restaurarse.
            await File.WriteAllTextAsync(Path.Combine(destination, "complete.json"), JsonSerializer.Serialize(new
            { Status = "Complete", CreatedAtUtc = DateTime.UtcNow, Files = manifest }), ct);
            ClinicalOperations.Audit(db, actor, "clinical.snapshot.completed", null);
            await db.SaveChangesAsync(ct);
            return new(destination, files.Length, "Complete");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await using var audit = await factory.CreateDbContextAsync(CancellationToken.None);
            ClinicalOperations.Audit(audit, actor, "clinical.snapshot.failed", null);
            await audit.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        finally { gate.Semaphore.Release(); }
    }

    private static IEnumerable<string> PrivateFiles(string root)
    {
        if (File.GetAttributes(root).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("El respaldo no admite enlaces.");
        foreach (var entry in Directory.EnumerateFileSystemEntries(root))
        {
            var attrs = File.GetAttributes(entry);
            if (attrs.HasFlag(FileAttributes.ReparsePoint)) throw new IOException("El respaldo no admite enlaces.");
            if (attrs.HasFlag(FileAttributes.Directory)) { foreach (var file in PrivateFiles(entry)) yield return file; }
            else yield return entry;
        }
    }
    private static async Task VerifyDatabaseAsync(string databaseFile, string destination, CancellationToken ct)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databaseFile, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check";
        if ((string?)await command.ExecuteScalarAsync(ct) != "ok") throw new IOException("La copia SQLite no pasó integridad.");
        command.CommandText = "PRAGMA foreign_key_check";
        await using (var errors = await command.ExecuteReaderAsync(ct))
            if (await errors.ReadAsync(ct)) throw new IOException("La copia tiene relaciones inválidas.");
        command.CommandText = "SELECT Id, Length, Sha256 FROM ClinicalAttachments WHERE State IN (1, 3)";
        await using var documents = await command.ExecuteReaderAsync(ct);
        while (await documents.ReadAsync(ct))
        {
            var file = Path.Combine(destination, "attachments", "clinical", Guid.Parse(documents.GetString(0)).ToString("N") + ".bin");
            if (!File.Exists(file) || new FileInfo(file).Length != documents.GetInt64(1)) throw new IOException("Falta un original en la copia.");
            await using var content = File.OpenRead(file);
            if (Convert.ToHexString(await SHA256.HashDataAsync(content, ct)) != documents.GetString(2)) throw new IOException("Un documento copiado no conserva su integridad.");
        }
    }
}
