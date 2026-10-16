using System.Security.Cryptography;
using System.Text.Json;
using OpticaSosa.Infrastructure.Clinical;
using OpticaSosa.Infrastructure.Persistence;
using OpticaSosa.Application.Clinical;
using Microsoft.Extensions.Options;

namespace OpticaSosa.Infrastructure.Backups;

public sealed class BackupVerifier(StoragePaths paths, IStorageCapacity capacity, IOptions<BackupRuntimeOptions> options)
{
    public async Task<DateTime> VerifyAsync(string directory, CancellationToken ct = default)
    {
        try { return await VerifyCoreAsync(directory, ct); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException or FormatException)
        { throw new InvalidDataException("El manifiesto no es válido o no es compatible.", ex); }
    }

    private static async Task<DateTime> VerifyCoreAsync(string directory, CancellationToken ct)
    {
        BackupFileSafety.RejectLinks(directory);
        var manifestPath = BackupFileSafety.Child(directory, "complete.json");
        if (!File.Exists(manifestPath) || new FileInfo(manifestPath).Length > 64L * 1024 * 1024)
            throw new IOException("No hay manifiesto completo válido.");
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath, ct));
        var root = manifest.RootElement;
        if (root.GetProperty("Status").GetString() != "Complete" || root.GetProperty("Format").GetInt32() != 2)
            throw new IOException("Formato de copia no compatible.");
        var files = root.GetProperty("Files");
        var createdAt = root.GetProperty("CreatedAtUtc").GetDateTime().ToUniversalTime();
        foreach (var name in new[] { "optica.db", "configuration.json", "recovery.txt" })
            if (!files.TryGetProperty(name, out _)) throw new IOException("Falta material de recuperación.");
        foreach (var item in files.EnumerateObject())
        {
            var path = BackupFileSafety.Child(directory, item.Name);
            await using var input = File.OpenRead(path);
            if (Convert.ToHexString(await SHA256.HashDataAsync(input, ct)) != item.Value.GetString())
                throw new IOException("Un archivo de la copia no conserva su hash.");
        }
        await ClinicalSnapshotWriter.VerifyDatabaseAsync(Path.Combine(directory, "optica.db"), directory, ct);
        return createdAt;
    }

    public async Task<string> RestoreIsolatedAsync(string directory, CancellationToken ct = default)
    {
        await VerifyAsync(directory, ct);
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "complete.json"), ct));
        var required = manifest.RootElement.GetProperty("Files").EnumerateObject()
            .Sum(x => new FileInfo(BackupFileSafety.Child(directory, x.Name)).Length);
        if (capacity.AvailableBytes < required + options.Value.ReserveBytes) throw new IOException("No hay espacio para restaurar en el entorno aislado.");
        var root = BackupFileSafety.Child(paths.DataDirectory, "restore-tests");
        Directory.CreateDirectory(root);
        var target = BackupFileSafety.Child(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(target);
        if (OperatingSystem.IsWindows()) StorageProtection.RestrictToCurrentWindowsUser(target);
        foreach (var file in manifest.RootElement.GetProperty("Files").EnumerateObject())
        {
            var destination = BackupFileSafety.Child(target, file.Name);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using var source = File.OpenRead(BackupFileSafety.Child(directory, file.Name));
            await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            await source.CopyToAsync(output, ct);
        }
        File.Copy(Path.Combine(directory, "complete.json"), Path.Combine(target, "complete.json"), overwrite: false);
        await VerifyAsync(target, ct);
        // Nunca iniciar automáticamente un servidor restaurado, migrar o cambiar rutas activas.
        return target;
    }
}
