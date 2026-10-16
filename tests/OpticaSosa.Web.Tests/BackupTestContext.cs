using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpticaSosa.Application.Backups;
using OpticaSosa.Infrastructure.Backups;

namespace OpticaSosa.Web.Tests;

internal sealed class BackupClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class FakeBackupTargets : IBackupTargets
{
    public string Root { get; set; } = "";
    public string ServerRoot { get; set; } = "";
    public bool Connected { get; set; } = true;
    public long FreeBytes { get; set; } = long.MaxValue;
    public int Preparations { get; private set; }
    public const string Volume = "ficticio-volume-guid";
    public string VolumeId { get; set; } = Volume;
    public IReadOnlyList<BackupTarget> Inspect() => Connected ? [new(VolumeId, "Disco ficticio", "NTFS", "Disco ficticio independiente",
        long.MaxValue, FreeBytes, true, true, "Sólo prueba aislada; no representa hardware real")] : [];
    public ResolvedBackupVolume ResolveVolume(string volumeId)
    {
        if (!Connected || volumeId != VolumeId) throw new IOException("Disco ficticio desconectado.");
        return new(ServerRoot, Inspect().Single());
    }
    private string Parent(string relativeFolder) => relativeFolder == "Respaldos" ? Root : BackupFileSafety.Child(Root, relativeFolder);
    public Guid Bind(string volumeId, Guid installationId, string relativeFolder)
    {
        if (!Connected || volumeId != VolumeId) throw new IOException("Disco ficticio desconectado.");
        var parent = Parent(relativeFolder); Directory.CreateDirectory(parent);
        return BackupTargetBinding.Bind(parent, volumeId, installationId);
    }
    public ResolvedBackupTarget Resolve(string volumeId, Guid installationId, bool prepare,
        string relativeFolder = "Respaldos", Guid? bindingId = null)
    {
        if (!Connected || volumeId != VolumeId) throw new IOException("Disco ficticio desconectado.");
        var parent = Parent(relativeFolder);
        BackupTargetBinding.Validate(parent, volumeId, installationId, bindingId);
        var root = Path.Combine(parent, installationId.ToString("N"));
        if (prepare) { Preparations++; Directory.CreateDirectory(root); }
        return new(root, FreeBytes);
    }
}

internal sealed class BackupTestContext : IAsyncDisposable
{
    public SecurityApp App { get; }
    public FakeBackupTargets Targets { get; } = new();
    public BackupClock Clock { get; } = new();
    public BackupCoordinator Coordinator => App.Services.GetRequiredService<BackupCoordinator>();
    public BackupTestContext(bool authorized = true, string? root = null, bool preserveData = false,
        Action<IServiceCollection>? configureServices = null)
    {
        App = new SecurityApp(root, preserveData, configureServices: services =>
        {
            services.AddSingleton<IBackupTargets>(Targets);
            services.AddSingleton<TimeProvider>(Clock);
            services.Configure<BackupRuntimeOptions>(o => { o.ExternalWritesAuthorized = authorized; o.WorkerEnabled = false; });
            configureServices?.Invoke(services);
        });
        Targets.Root = Path.Combine(App.Root, "external-ficticio");
        Targets.ServerRoot = Path.Combine(App.Root, "server-folders-ficticio");
    }
    public async Task InitializeAsync()
    {
        await App.InitializeAsync();
        Directory.CreateDirectory(Path.Combine(Targets.ServerRoot, "Respaldos"));
        if (OperatingSystem.IsWindows()) OpticaSosa.Infrastructure.Persistence.StorageProtection.RestrictToCurrentWindowsUser(Targets.ServerRoot);
    }
    public async Task<BackupAction> ConfigureAsync(bool enabled = false, int keep = 30, int days = 1)
    {
        var view = await Coordinator.OverviewAsync(default);
        var result = await Coordinator.ConfigureAsync(new(false, days, "13:00", "UTC", Targets.VolumeId, keep,
            view.Configuration.Version), "admin.ficticio", default);
        if (!result.Succeeded) return result;
        view = await Coordinator.OverviewAsync(default);
        if (!view.Configuration.DestinationBound && view.ExternalWritesAuthorized)
        {
            result = await Coordinator.BindDestinationAsync(view.Configuration.Version, "admin.ficticio", default);
            if (!result.Succeeded) return result;
            view = await Coordinator.OverviewAsync(default);
        }
        return enabled ? await Coordinator.ConfigureAsync(view.Configuration with { Enabled = true }, "admin.ficticio", default) : result;
    }
    public async Task<BackupRunView> CopyAsync()
    {
        Xunit.Assert.True((await Coordinator.RequestAsync("admin.ficticio", default)).Succeeded);
        var queuedId = Xunit.Assert.Single((await Coordinator.OverviewAsync(default)).Runs, x => x.Status == "Queued").Id;
        await Coordinator.TickAsync();
        return (await Coordinator.OverviewAsync(default)).Runs.Single(x => x.Id == queuedId);
    }
    public ValueTask DisposeAsync() => App.DisposeAsync();
}
