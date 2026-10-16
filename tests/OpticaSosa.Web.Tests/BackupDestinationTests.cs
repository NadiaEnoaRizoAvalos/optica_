using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Infrastructure.Backups;
using OpticaSosa.Infrastructure.Persistence;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class BackupDestinationTests
{
    [Fact]
    public async Task BindingRequiresAuthorizationAndDoesNotActivateSchedule()
    {
        await using var test = new BackupTestContext(authorized: false); await test.InitializeAsync(); await test.ConfigureAsync();
        var config = (await test.Coordinator.OverviewAsync(default)).Configuration;
        Assert.False((await test.Coordinator.BindDestinationAsync(config.Version, "admin.ficticio", default)).Succeeded);
        Assert.False(Directory.Exists(test.Targets.Root)); Assert.False(config.Enabled); Assert.False(config.DestinationBound);
    }

    [Fact]
    public async Task AnotherOpticChoosesItsOwnFolderAndMustRebindAfterChanges()
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync(); await test.ConfigureAsync();
        var input = (await test.Coordinator.OverviewAsync(default)).Configuration with { RelativeFolder = "OtraOptica\\Copias" };
        Assert.True((await test.Coordinator.ConfigureAsync(input, "admin.ficticio", default)).Succeeded);
        var saved = (await test.Coordinator.OverviewAsync(default)).Configuration;
        Assert.Equal("OtraOptica/Copias", saved.RelativeFolder); Assert.False(saved.DestinationBound);
        Assert.False((await test.Coordinator.RequestAsync("admin.ficticio", default)).Succeeded);
        Assert.False((await test.Coordinator.ConfigureAsync(saved with { Enabled = true }, "admin.ficticio", default)).Succeeded);
        Assert.True((await test.Coordinator.BindDestinationAsync(saved.Version, "admin.ficticio", default)).Succeeded);
        var bound = (await test.Coordinator.OverviewAsync(default)).Configuration;
        Assert.True(bound.DestinationBound); Assert.False(bound.Enabled); Assert.Null((await test.Coordinator.OverviewAsync(default)).NextUtc);
        Assert.Equal("Complete", (await test.CopyAsync()).Status);
    }

    [Fact]
    public async Task SameVolumeGuidWithoutMarkerIsRejectedWithoutFallback()
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync(); await test.ConfigureAsync();
        await test.CopyAsync(); var correctRoot = test.Targets.Root;
        var other = Path.Combine(test.App.Root, "otro-disco-ficticio"); Directory.CreateDirectory(other); test.Targets.Root = other;
        Assert.Equal("Failed", (await test.CopyAsync()).Status);
        Assert.Empty(Directory.GetFileSystemEntries(other));
        Assert.NotEmpty(Directory.GetDirectories(correctRoot));
    }

    [Fact]
    public async Task ChangedMountPointKeepsBindingAndFindsPriorCopies()
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync(); await test.ConfigureAsync(); var run = await test.CopyAsync();
        var nextMount = Path.Combine(test.App.Root, "nueva-letra-ficticia");
        Assert.Equal(test.App.Root, Path.GetDirectoryName(test.Targets.Root)); Assert.Equal(test.App.Root, Path.GetDirectoryName(nextMount));
        Directory.Move(test.Targets.Root, nextMount); test.Targets.Root = nextMount;
        Assert.True((await test.Coordinator.VerifyAsync(run.Id, "admin.ficticio", default)).Succeeded);
    }

    [Fact]
    public async Task OldRunsKeepTheirFolderAndMarkerWhenCurrentDestinationChanges()
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync(); await test.ConfigureAsync(keep: 1);
        var old = await test.CopyAsync(); test.Clock.Now = test.Clock.Now.AddMinutes(1);
        var input = (await test.Coordinator.OverviewAsync(default)).Configuration with { RelativeFolder = "OtraCarpeta" };
        Assert.True((await test.Coordinator.ConfigureAsync(input, "admin.ficticio", default)).Succeeded);
        var config = (await test.Coordinator.OverviewAsync(default)).Configuration;
        Assert.True((await test.Coordinator.BindDestinationAsync(config.Version, "admin.ficticio", default)).Succeeded);
        Assert.Equal("Complete", (await test.CopyAsync()).Status);
        Assert.True((await test.Coordinator.VerifyAsync(old.Id, "admin.ficticio", default)).Succeeded);
        Assert.Equal(2, (await test.Coordinator.OverviewAsync(default)).Runs.Count(x => x.Status == "Complete"));
    }

    [Fact]
    public async Task InvalidOrForeignMarkerCannotBeOverwrittenByBinding()
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync(); await test.ConfigureAsync();
        var marker = Assert.Single(Directory.GetFiles(test.Targets.Root, ".optica-backup-target-*.json"));
        var original = await File.ReadAllTextAsync(marker);
        await using var db = await test.App.Services.GetRequiredService<IDbContextFactory<OpticaDbContext>>().CreateDbContextAsync();
        var settings = await db.BackupSettings.SingleAsync();
        Assert.Throws<IOException>(() => BackupTargetBinding.Validate(test.Targets.Root, FakeBackupTargets.Volume, settings.InstallationId, Guid.NewGuid()));
        Assert.Throws<IOException>(() => BackupTargetBinding.Validate(test.Targets.Root, FakeBackupTargets.Volume, Guid.NewGuid(), settings.BindingId));
        await File.WriteAllTextAsync(marker, "Marcador ficticio dañado");
        var version = (await test.Coordinator.OverviewAsync(default)).Configuration.Version;
        Assert.False((await test.Coordinator.BindDestinationAsync(version, "admin.ficticio", default)).Succeeded);
        Assert.Equal("Marcador ficticio dañado", await File.ReadAllTextAsync(marker));
        await File.WriteAllTextAsync(marker, original);
        Assert.True((await test.Coordinator.BindDestinationAsync(version, "admin.ficticio", default)).Succeeded);
    }
}
