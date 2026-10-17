using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpticaSosa.Infrastructure.Backups;
using OpticaSosa.Web.Backups;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class WindowsServicePreparationTests
{
    [Fact]
    public async Task ServicePlanUsesAbsoluteQuotedPathsAndAutomaticRecoveryWithoutChangingWindows()
    {
        var result = await PlanAsync([]);
        Assert.Equal(0, result.Exit);
        using var json = JsonDocument.Parse(result.Output);
        Assert.True(json.RootElement.GetProperty("PlanOnly").GetBoolean());
        Assert.Equal("Automatic", json.RootElement.GetProperty("Startup").GetString());
        var binary = json.RootElement.GetProperty("BinaryPath").GetString()!;
        Assert.Contains("--environment Production", binary);
        Assert.Contains("http://localhost:5080", binary);
        Assert.Contains("\"", binary);
        Assert.Contains("restart/60000", json.RootElement.GetProperty("Recovery").GetString());
    }

    [Fact]
    public async Task OperationalActionRequiresAuthorizationBeforeQueryingOrModifyingService()
    {
        var result = await PlanAsync(["-Action", "Install"]);
        Assert.NotEqual(0, result.Exit); Assert.Empty(result.Output);
        Assert.Contains("autorizaci", result.Error);
    }

    [Theory]
    [InlineData("relative\\OpticaSosa.Web.exe")]
    [InlineData("\\\\servidor\\publico\\OpticaSosa.Web.exe")]
    [InlineData("C:\\ruta\"inyectada\\OpticaSosa.Web.exe")]
    public async Task ServicePlanRejectsAmbiguousOrNonlocalExecutable(string executable)
    {
        Assert.NotEqual(0, (await PlanAsync([], executable)).Exit);
    }

    [Fact]
    public async Task RecreatedBackgroundWorkerRecoversMissedScheduleOnceWithoutBrowser()
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync(); await test.ConfigureAsync(enabled: true);
        test.Clock.Now = test.Clock.Now.AddHours(2);
        using (var first = Worker(test))
        { await first.StartAsync(default); await WaitAsync(test, 1); await first.StopAsync(default); }
        test.Clock.Now = test.Clock.Now.AddDays(4);
        using (var restarted = Worker(test))
        { await restarted.StartAsync(default); await WaitAsync(test, 2); await restarted.StopAsync(default); }
        await test.Coordinator.TickAsync();
        var overview = await test.Coordinator.OverviewAsync(default);
        Assert.Equal(2, overview.Runs.Count(x => x.Status == "Complete"));
        Assert.Contains(overview.Runs, x => x.Trigger == "Recovery" && x.MissedPeriods > 0);
        Assert.True(overview.NextUtc > test.Clock.Now.UtcDateTime);
    }

    private static BackupWorker Worker(BackupTestContext test) => new(test.Coordinator,
        Options.Create(new BackupRuntimeOptions { WorkerEnabled = true }), TimeProvider.System, NullLogger<BackupWorker>.Instance);
    private static async Task WaitAsync(BackupTestContext test, int count)
    {
        var until = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < until)
        {
            if ((await test.Coordinator.OverviewAsync(default)).Runs.Count(x => x.Status == "Complete") >= count) return;
            await Task.Delay(100);
        }
        Assert.Fail("El worker ficticio no completó su ejecución.");
    }
    private static async Task<(int Exit, string Output, string Error)> PlanAsync(string[] extra, string? executable = null)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "OpticaSosa.sln"))) root = root.Parent;
        Assert.NotNull(root);
        var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"))
        { WorkingDirectory = Environment.SystemDirectory, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-File", Path.Combine(root.FullName, "scripts", "Manage-OpticaService.ps1"),
            "-Executable", executable ?? Path.Combine(Path.GetTempPath(), "Optica instalacion ficticia", "OpticaSosa.Web.exe"),
            "-DataDirectory", Path.Combine(Path.GetTempPath(), "Optica datos ficticios") }.Concat(extra)) info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        return (process.ExitCode, await output, await error);
    }
}
