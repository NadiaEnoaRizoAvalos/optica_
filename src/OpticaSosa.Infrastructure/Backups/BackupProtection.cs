using System.Diagnostics;
using System.Text;
using System.Text.Json;
using OpticaSosa.Application.Backups;
using OpticaSosa.Application.Security;

namespace OpticaSosa.Infrastructure.Backups;

public interface IBackupProtectionProbe
{
    Task<BackupProtectionReport> InspectAsync(CancellationToken ct = default);
}

public sealed class BackupProtectionService(IBackupProtectionProbe probe, ICurrentUser user) : IBackupProtectionService
{
    public Task<BackupProtectionReport> InspectAsync(CancellationToken ct = default)
    {
        if (user.Id is null || !user.HasPermission(Permissions.ManageBackups)) throw new UnauthorizedAccessException();
        return probe.InspectAsync(ct);
    }
}

public sealed class WindowsBackupProtectionProbe : IBackupProtectionProbe
{
    public async Task<BackupProtectionReport> InspectAsync(CancellationToken ct = default)
    {
        var unknown = new BackupProtectionReport("No comprobada", false, "No comprobado", "No comprobado", 0, [],
            "No se pudo verificar BitLocker o el servicio. Un estado desconocido no acredita protección; revisar con un operador autorizado.");
        if (!OperatingSystem.IsWindows()) return unknown;
        using var resource = typeof(WindowsBackupProtectionProbe).Assembly.GetManifestResourceStream(
            "OpticaSosa.Infrastructure.Deployment.InspectWindowsProtection.ps1")!;
        using var reader = new StreamReader(resource);
        var script = await reader.ReadToEndAsync(ct);
        var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(script)) })
            info.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = info };
        var started = false;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(12));
        try
        {
            process.Start(); started = true;
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errors = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            var json = await output; await errors;
            if (process.ExitCode != 0 || json.Length > 32768) return unknown;
            return JsonSerializer.Deserialize<BackupProtectionReport>(json) ?? unknown;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return unknown; }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or JsonException) { return unknown; }
        finally { if (started && !process.HasExited) process.Kill(entireProcessTree: true); }
    }
}

public sealed class BackupProtectionGuard(IBackupProtectionProbe probe)
{
    public async Task DemandAsync(string volumeId, CancellationToken ct)
    {
        var report = await probe.InspectAsync(ct);
        var matches = report.Volumes.Where(x => string.Equals(x.VolumeId, volumeId, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (!report.ProviderAvailable || matches.Length != 1 || !matches[0].Protected)
            throw new InvalidDataException("El destino no tiene cifrado completo, protección activa, desbloqueo y protector de recuperación comprobados. No se copian datos en claro.");
    }
}
