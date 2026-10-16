using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Infrastructure.Backups;
using OpticaSosa.Infrastructure.Persistence;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class BackupBindingMigrationTests
{
    [Fact]
    public async Task UpgradePreservesOldDestinationButRequiresExplicitBindingAndReactivation()
    {
        await using var app = new SecurityApp(); using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpticaDbContext>();
        await db.GetService<IMigrator>().MigrateAsync("20261016055719_AutomaticBackups");
        var id = Guid.NewGuid(); var version = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO BackupSettings (Id, InstallationId, Enabled, EveryDays, LocalTime, TimeZoneId, VolumeId, KeepCopies, Version, NextUtc) VALUES (1,{id},1,1,'13:00','UTC','volumen-ficticio',5,{version},'2026-10-16 13:00:00')");
        Assert.False(await app.Services.GetRequiredService<BackupCoordinator>().ReadyAsync(default));
        await db.Database.MigrateAsync();
        var settings = await db.BackupSettings.SingleAsync();
        Assert.Equal(id, settings.InstallationId); Assert.Equal("volumen-ficticio", settings.VolumeId);
        Assert.Equal(5, settings.KeepCopies); Assert.Equal("OpticaSosaBackups", settings.RelativeFolder);
        Assert.False(settings.Enabled); Assert.Null(settings.NextUtc); Assert.Null(settings.BindingId);
        Assert.True(await app.Services.GetRequiredService<BackupCoordinator>().ReadyAsync(default));
    }
}
