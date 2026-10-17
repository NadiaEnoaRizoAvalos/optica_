using System.Security.Cryptography;
using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Infrastructure.Backups;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Viewer;

public sealed class ViewerSnapshot : IDisposable
{
    private readonly ConcurrentBag<FileStream> leases = [];
    public string Workspace { get; private set; } = "";
    public string Directory { get; private set; } = "";
    public DateTime CreatedUtc { get; private set; }
    public string ConnectionString => new SqliteConnectionStringBuilder
    {
        DataSource = new Uri(Path.Combine(Directory, "optica.db")).AbsoluteUri + "?immutable=1",
        Mode = SqliteOpenMode.ReadOnly, Pooling = false, Cache = SqliteCacheMode.Private
    }.ToString();

    public async Task OpenAsync(string source, string workspace, BackupVerifier verifier,
        BackupAuthenticity authenticity, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("El visor requiere Windows.");
        source = Path.GetFullPath(source); workspace = Path.GetFullPath(workspace);
        BackupFileSafety.RejectLinks(source); BackupFileSafety.RejectLinks(workspace);
        if (System.IO.Directory.Exists(workspace) || File.Exists(workspace))
            throw new IOException("Elegí una carpeta de trabajo nueva. No se sobrescriben datos.");
        if (workspace.StartsWith(source.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            source.StartsWith(workspace.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("El trabajo debe estar separado del respaldo.");
        var parent = Path.GetDirectoryName(workspace)!;
        if (!System.IO.Directory.Exists(parent)) throw new IOException("La carpeta padre debe existir y ser privada.");
        var drive = new DriveInfo(Path.GetPathRoot(workspace)!);
        if (drive.DriveType != DriveType.Fixed || drive.DriveFormat != "NTFS")
            throw new IOException("Elegí almacenamiento local fijo NTFS.");
        WindowsBackupTargets.RequirePrivateAcl(parent); // Sólo lectura: nunca cambia ACL.

        // No abrir SQLite del original. Autenticar primero el manifiesto y copiar como
        // material no confiable a una carpeta nueva; verificar esa copia antes de consultar.
        var manifest = BackupFileSafety.Child(source, "complete.json");
        var signature = BackupFileSafety.Child(source, BackupAuthenticity.SignatureFile);
        using var manifestLease = File.Open(manifest, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var signatureLease = File.Open(signature, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (manifestLease.Length > 64L * 1024 * 1024) throw new IOException("Manifiesto demasiado grande.");
        var bytes = await File.ReadAllBytesAsync(manifest, ct);
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        if (root.GetProperty("Format").GetInt32() != 3 || root.GetProperty("Status").GetString() != "Complete")
            throw new InvalidDataException("El visor exige un respaldo completo firmado de formato 3.");
        await authenticity.VerifyAsync(source, bytes, 3, ct);
        var files = root.GetProperty("Files").EnumerateObject().ToArray();
        long size = manifestLease.Length + signatureLease.Length;
        foreach (var file in files) size = checked(size + new FileInfo(BackupFileSafety.Child(source, file.Name)).Length);
        if (drive.AvailableFreeSpace < size + 512L * 1024 * 1024) throw new IOException("Espacio insuficiente para consultar la copia.");
        System.IO.Directory.CreateDirectory(workspace);
        WindowsBackupTargets.RequirePrivateAcl(workspace);
        Workspace = workspace; Directory = Path.Combine(workspace, "snapshot");
        System.IO.Directory.CreateDirectory(Directory);
        foreach (var name in files.Select(x => x.Name).Append("complete.json").Append(BackupAuthenticity.SignatureFile))
        {
            ct.ThrowIfCancellationRequested();
            var target = BackupFileSafety.Child(Directory, name);
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using var input = File.Open(BackupFileSafety.Child(source, name), FileMode.Open, FileAccess.Read, FileShare.Read);
            await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            await input.CopyToAsync(output, ct);
        }
        CreatedUtc = await verifier.VerifyAsync(Directory, ct);
        var wal = Path.Combine(Directory, "optica.db-wal");
        if (File.Exists(wal) && new FileInfo(wal).Length > 0)
            throw new InvalidDataException("La copia requiere un WAL no consolidado; no se consulta como snapshot inmutable.");
        var options = new DbContextOptionsBuilder<OpticaDbContext>().UseSqlite(ConnectionString).Options;
        await using (var db = new OpticaDbContext(options))
        {
            var applied = (await db.Database.GetAppliedMigrationsAsync(ct)).ToArray();
            if (!applied.SequenceEqual(db.Database.GetMigrations()))
                throw new InvalidDataException("Versión de base incompatible. Usá un visor compatible; no se ejecutan migraciones.");
        }
        // immutable sólo es válido mientras estos archivos no cambien: leases Windows
        // niegan escritura y reemplazo durante toda la sesión, sin alterar permisos.
        try
        {
            foreach (var file in files.Select(x => x.Name).Append("complete.json").Append(BackupAuthenticity.SignatureFile))
                leases.Add(File.Open(BackupFileSafety.Child(Directory, file), FileMode.Open, FileAccess.Read, FileShare.Read));
            // Volver a comprobar los hashes después de adquirir los handles.
            foreach (var file in files)
            {
                await using var input = File.OpenRead(BackupFileSafety.Child(Directory, file.Name));
                if (Convert.ToHexString(await SHA256.HashDataAsync(input, ct)) != file.Value.GetString())
                    throw new InvalidDataException("La copia aislada cambió durante su preparación.");
            }
        }
        catch { Dispose(); throw; }
    }

    public void Dispose() { while (leases.TryTake(out var stream)) stream.Dispose(); }
}
