using OpticaSosa.Infrastructure.Identity;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Web.Security;

public static class LocalCommands
{
    public static async Task<bool> TryRunAsync(WebApplication app, string[] args)
    {
        var command = args.FirstOrDefault(value => value is "--initialize-database" or "--create-admin" or "--recover-admin");
        if (command is null) return false;
        using var scope = app.Services.CreateScope();
        var provisioning = scope.ServiceProvider.GetRequiredService<LocalProvisioning>();
        if (command == "--initialize-database")
        {
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("El servidor requiere Windows.");
            StorageProtection.RestrictToCurrentWindowsUser(scope.ServiceProvider.GetRequiredService<StoragePaths>().DataDirectory);
            await provisioning.InitializeAsync();
            Console.WriteLine("Base de datos y roles inicializados. No se crearon cuentas ni contraseñas.");
            return true;
        }
        if (Console.IsInputRedirected)
            throw new InvalidOperationException("La creación y recuperación requieren una terminal local interactiva.");

        Console.Write("Nombre de usuario: ");
        var userName = Console.ReadLine() ?? string.Empty;
        Console.WriteLine("Contraseña: mínimo 12 caracteres, mayúscula, minúscula, número y símbolo.");
        Console.Write("Contraseña (no se muestra): ");
        var password = ReadSecret();
        Console.Write("Repetir contraseña: ");
        var confirmation = ReadSecret();
        if (password != confirmation) throw new InvalidOperationException("Las contraseñas no coinciden.");
        var result = command == "--create-admin"
            ? await provisioning.CreateFirstAdminAsync(userName, password)
            : await provisioning.RecoverAdminAsync(userName, password);
        if (!result.Succeeded) throw new InvalidOperationException(result.Error);
        Console.WriteLine("Operación completada. Las sesiones anteriores fueron revocadas si se recuperó el acceso.");
        return true;
    }

    private static string ReadSecret()
    {
        var secret = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return secret.ToString(); }
            if (key.Key == ConsoleKey.Backspace) { if (secret.Length > 0) secret.Length--; }
            else if (!char.IsControl(key.KeyChar) && secret.Length < 256) secret.Append(key.KeyChar);
        }
    }
}
