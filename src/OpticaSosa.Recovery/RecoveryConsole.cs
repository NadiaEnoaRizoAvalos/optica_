using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using OpticaSosa.Infrastructure.Backups;
using OpticaSosa.Infrastructure.Clinical;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Recovery;

public static class RecoveryConsole
{
    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            if (args.Length == 0 || args[0] == "--help")
            {
                Console.WriteLine("verify|restore|inventory --backup RUTA --trust HUELLA_SHA256 --workspace CARPETA_VACIA; check-key --pfx ARCHIVO --trust HUELLA_SHA256. Contraseña sólo interactiva; nunca argumentos.");
                return 0;
            }
            if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("La recuperación requiere Windows.");
            if (args.Length == 0 || args[0] is not ("verify" or "restore" or "inventory" or "check-key"))
                throw new ArgumentException("Uso: verify|restore --backup RUTA --trust HUELLA --workspace CARPETA_VACIA; check-key --pfx ARCHIVO --trust HUELLA. Sin contraseñas por argumentos.");
            var options = Parse(args[1..]);
            var fingerprint = Required(options, "--trust");
            if (fingerprint.Length != 64 || !fingerprint.All(Uri.IsHexDigit)) throw new ArgumentException("La huella debe ser SHA-256 hexadecimal de 64 caracteres.");
            if (args[0] == "check-key")
            {
                if (options.Keys.Any(x => x is not ("--pfx" or "--trust"))) throw new ArgumentException("Opción no permitida.");
                if (Console.IsInputRedirected) throw new InvalidOperationException("Ingresá la contraseña en una terminal interactiva local; no usar tuberías, argumentos ni logs.");
                var pfx = Path.GetFullPath(Required(options, "--pfx")); BackupFileSafety.RejectLinks(Path.GetDirectoryName(pfx)!);
                if (File.GetAttributes(pfx).HasFlag(FileAttributes.ReparsePoint) || new FileInfo(pfx).Length > 1024 * 1024)
                    throw new InvalidDataException("PFX no permitido.");
                Console.Write("Contraseña del PFX (oculta): "); var password = ReadSecret();
                try { RecoveryKeyCheck.Verify(await File.ReadAllBytesAsync(pfx), password, fingerprint); }
                finally { Array.Clear(password); }
                Console.WriteLine("Clave recuperable comprobada en memoria. No se instaló ni persistió."); return 0;
            }
            if (options.Keys.Any(x => x is not ("--backup" or "--trust" or "--workspace"))) throw new ArgumentException("Opción no permitida.");
            var source = Path.GetFullPath(Required(options, "--backup"));
            var workspace = Path.GetFullPath(Required(options, "--workspace"));
            BackupFileSafety.RejectLinks(source); BackupFileSafety.RejectLinks(workspace);
            if (!Directory.Exists(workspace) || Directory.EnumerateFileSystemEntries(workspace).Any() ||
                Path.GetPathRoot(workspace) == workspace || Within(source, workspace) || Within(workspace, source))
                throw new InvalidDataException("Elegí un directorio de trabajo vacío, existente y separado de la copia y de cualquier instalación activa.");
            var paths = new StoragePaths(workspace, Path.Combine(AppContext.BaseDirectory, "wwwroot"));
            var runtime = Options.Create(new BackupRuntimeOptions { RequireSignature = true,
                TrustedSigningCertificatesSha256 = [fingerprint], WorkerEnabled = false });
            var authenticity = new BackupAuthenticity(runtime, new WindowsBackupSigningCertificate(runtime), TimeProvider.System);
            var verifier = new BackupVerifier(paths, new StorageCapacity(paths), runtime, authenticity);
            await verifier.VerifyAsync(source);
            if (args[0] == "inventory")
            {
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(await BackupInventory.ReadAsync(source)));
                return 0;
            }
            if (args[0] == "restore") Console.WriteLine("Restauración aislada verificada: " + await verifier.RestoreIsolatedAsync(source));
            else Console.WriteLine("Firma, hashes, SQLite, relaciones y originales verificados. No se escribió en la copia.");
            Console.WriteLine("No se inició servidor ni se activó programación. Esta herramienta local no es el visor de emergencia.");
            return 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or CryptographicException or ArgumentException or InvalidOperationException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        {
            Console.Error.WriteLine("No se completó la recuperación. Revisá parámetros, firma/confianza, contraseña, integridad, permisos y espacio. No se reemplazó la base activa.");
            return 1;
        }
    }

    private static bool Within(string candidate, string root) => candidate.Equals(root, StringComparison.OrdinalIgnoreCase) ||
        candidate.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    private static Dictionary<string, string> Parse(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (args.Length % 2 != 0) throw new ArgumentException("Cada opción necesita un valor.");
        for (var i = 0; i < args.Length; i += 2)
            if (!result.TryAdd(args[i], args[i + 1])) throw new ArgumentException("Opción duplicada.");
        return result;
    }
    private static string Required(Dictionary<string, string> args, string key) =>
        args.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("Falta opción requerida.");
    private static char[] ReadSecret()
    {
        var buffer = new char[256]; var length = 0;
        try
        {
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return buffer[..length]; }
                if (key.Key == ConsoleKey.Backspace) { if (length > 0) buffer[--length] = '\0'; }
                else if (!char.IsControl(key.KeyChar) && length < buffer.Length) buffer[length++] = key.KeyChar;
            }
        }
        finally { Array.Clear(buffer); }
    }
}
