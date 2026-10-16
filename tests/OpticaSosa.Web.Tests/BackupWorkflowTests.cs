using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Domain.Backups;
using OpticaSosa.Infrastructure.Backups;
using OpticaSosa.Infrastructure.Persistence;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class BackupWorkflowTests
{
    [Fact]
    public async Task DefaultsAreDisabledWithoutAnHourAndAuthorizationCannotBeBypassed()
    {
        await using var test = new BackupTestContext(authorized: false); await test.InitializeAsync();
        var defaults = await test.Coordinator.OverviewAsync(default);
        Assert.False(defaults.Configuration.Enabled); Assert.Null(defaults.Configuration.LocalTime); Assert.Null(defaults.NextUtc);
        Assert.True((await test.ConfigureAsync()).Succeeded);
        Assert.False((await test.ConfigureAsync(enabled: true)).Succeeded);
        Assert.False((await test.Coordinator.RequestAsync("admin.ficticio", default)).Succeeded);
        await test.Coordinator.TickAsync();
        Assert.False(Directory.Exists(test.Targets.Root)); Assert.Equal(0, test.Targets.Preparations);
    }

    [Fact]
    public async Task ManualCopyWorksWhenScheduleIsDisabledAndRestoresConfiguration()
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync(); Assert.True((await test.ConfigureAsync()).Succeeded);
        var run = await test.CopyAsync(); Assert.Equal("Complete", run.Status);
        var dbFactory = test.App.Services.GetRequiredService<IDbContextFactory<OpticaDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();
        var row = await db.BackupRuns.SingleAsync(x => x.Id == run.Id);
        var settings = await db.BackupSettings.SingleAsync();
        var copy = Path.Combine(test.Targets.Root, settings.InstallationId.ToString("N"), row.DirectoryName!);
        var sourceHash = await File.ReadAllBytesAsync(Path.Combine(copy, "optica.db"));
        var target = await test.App.Services.GetRequiredService<BackupVerifier>().RestoreIsolatedAsync(copy);
        Assert.Equal(sourceHash, await File.ReadAllBytesAsync(Path.Combine(target, "optica.db")));
        Assert.True(File.Exists(Path.Combine(target, "configuration.json"))); Assert.NotEqual(copy, target);
        var options = new DbContextOptionsBuilder<OpticaDbContext>().UseSqlite($"Data Source={Path.Combine(target, "optica.db")};Mode=ReadOnly;Pooling=False").Options;
        await using var restored = new OpticaDbContext(options);
        Assert.Equal(3, await restored.Users.CountAsync()); Assert.False((await restored.BackupSettings.SingleAsync()).Enabled);
        Assert.True((await test.Coordinator.VerifyAsync(run.Id, "admin.ficticio", default)).Succeeded);
    }

    [Fact]
    public async Task MissedDaysProduceOneCurrentCopyAndNoDuplicateAfterRestart()
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync(); Assert.True((await test.ConfigureAsync(enabled: true)).Succeeded);
        test.Clock.Now = test.Clock.Now.AddDays(4).AddHours(2);
        await test.Coordinator.TickAsync(); await test.Coordinator.TickAsync();
        var view = await test.Coordinator.OverviewAsync(default);
        var run = Assert.Single(view.Runs); Assert.Equal("Complete", run.Status); Assert.Equal("Recovery", run.Trigger);
        Assert.Equal(4, run.MissedPeriods); Assert.True(view.NextUtc > test.Clock.Now.UtcDateTime);
    }

    [Fact]
    public async Task DisconnectedDiskFailsWithoutFallbackAndRetrySucceeds()
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync(); await test.ConfigureAsync(enabled: true);
        test.Clock.Now = test.Clock.Now.AddHours(2); test.Targets.Connected = false;
        await test.Coordinator.TickAsync();
        var failed = await test.Coordinator.OverviewAsync(default);
        Assert.Equal("Failed", Assert.Single(failed.Runs).Status); Assert.NotNull(failed.Alert); Assert.False(Directory.Exists(test.Targets.Root));
        await test.Coordinator.TickAsync(); Assert.Single((await test.Coordinator.OverviewAsync(default)).Runs);
        test.Targets.Connected = true; test.Clock.Now = test.Clock.Now.AddMinutes(16);
        await test.Coordinator.TickAsync(); Assert.Contains((await test.Coordinator.OverviewAsync(default)).Runs, x => x.Status == "Complete");
    }

    [Fact]
    public async Task FullDestinationKeepsThePreviousValidCopy()
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync(); await test.ConfigureAsync(keep: 1);
        var valid = await test.CopyAsync(); test.Clock.Now = test.Clock.Now.AddMinutes(1);
        test.Targets.FreeBytes = 1;
        Assert.Equal("Failed", (await test.CopyAsync()).Status);
        Assert.Contains((await test.Coordinator.OverviewAsync(default)).Runs, x => x.Id == valid.Id && x.Status == "Complete");
    }

    [Fact]
    public async Task RetentionPreservesLastValidAndOnlyDeletesOwnedDirectories()
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync(); await test.ConfigureAsync(keep: 1);
        await test.CopyAsync(); test.Clock.Now = test.Clock.Now.AddMinutes(1); await test.CopyAsync();
        var view = await test.Coordinator.OverviewAsync(default);
        Assert.Single(view.Runs, x => x.Status == "Complete"); Assert.Single(view.Runs, x => x.Status == "Pruned");
        Assert.Single(Directory.GetDirectories(Assert.Single(Directory.GetDirectories(test.Targets.Root))));
    }

    [Fact]
    public async Task ParallelManualRequestsAndTicksDoNotDuplicateCopies()
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync(); await test.ConfigureAsync();
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => test.Coordinator.RequestAsync("admin.ficticio", default)));
        Assert.Single(results, x => x.Succeeded);
        await Task.WhenAll(test.Coordinator.TickAsync(), test.Coordinator.TickAsync(), test.Coordinator.TickAsync());
        Assert.Equal("Complete", Assert.Single((await test.Coordinator.OverviewAsync(default)).Runs).Status);
    }

    [Fact]
    public async Task InterruptedManualJobGetsANewDirectoryAndIsRecovered()
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync(); await test.ConfigureAsync();
        await using var db = await test.App.Services.GetRequiredService<IDbContextFactory<OpticaDbContext>>().CreateDbContextAsync();
        db.Add(new BackupRun { StartedUtc = test.Clock.Now.UtcDateTime.AddMinutes(-1), Status = "Running", VolumeId = FakeBackupTargets.Volume });
        await db.SaveChangesAsync(); await test.Coordinator.TickAsync();
        var rows = (await test.Coordinator.OverviewAsync(default)).Runs;
        Assert.Contains(rows, x => x.Status == "Complete"); Assert.Contains(rows, x => x.Status == "Failed");
    }

    [Fact]
    public async Task CorruptCopyFailsRestorationWithoutReplacingActiveDatabase()
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync(); await test.ConfigureAsync();
        var run = await test.CopyAsync();
        var directory = Assert.Single(Directory.GetDirectories(Assert.Single(Directory.GetDirectories(test.Targets.Root))));
        await File.AppendAllTextAsync(Path.Combine(directory, "optica.db"), "Corrupción ficticia");
        Assert.False((await test.Coordinator.VerifyAsync(run.Id, "admin.ficticio", default)).Succeeded);
        await using var db = await test.App.Services.GetRequiredService<IDbContextFactory<OpticaDbContext>>().CreateDbContextAsync();
        Assert.Equal(3, await db.Users.CountAsync());
    }

    [Fact]
    public async Task StaleConfigurationCannotOverwriteNewSettings()
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync(); await test.ConfigureAsync();
        var old = (await test.Coordinator.OverviewAsync(default)).Configuration;
        Assert.True((await test.ConfigureAsync(days: 7)).Succeeded);
        Assert.False((await test.Coordinator.ConfigureAsync(old, "admin.ficticio", default)).Succeeded);
        Assert.Equal(7, (await test.Coordinator.OverviewAsync(default)).Configuration.EveryDays);
    }
}
