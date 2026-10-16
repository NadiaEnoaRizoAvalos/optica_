using System.Text.Encodings.Web;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class BackupAdministrationTests
{
    [Theory]
    [InlineData("15:30", 20, 15, 30)]
    [InlineData("11:15", 21, 11, 15)]
    public async Task ChangingHourRecalculatesNextExecution(string hour, int day, int nextHour, int minute)
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync();
        Assert.True((await test.ConfigureAsync(enabled: true)).Succeeded);
        var before = await test.Coordinator.OverviewAsync(default);
        Assert.True((await test.Coordinator.ConfigureAsync(before.Configuration with { LocalTime = hour },
            "admin.ficticio", default)).Succeeded);
        var after = await test.Coordinator.OverviewAsync(default);
        Assert.Equal(new DateTime(2026, 10, day, nextHour, minute, 0, DateTimeKind.Utc), after.NextUtc);
        Assert.NotEqual(before.NextUtc, after.NextUtc);
        using var client = test.App.Browser(); await BrowserForms.LoginAsync(client, "admin.ficticio");
        var html = await client.GetStringAsync("/admin/respaldos");
        Assert.Contains($"{day}/10/2026 {nextHour:00}:{minute:00}:00", html);
    }

    [Fact]
    public async Task EditingDisabledScheduleDoesNotActivateItAndIncompleteActivationIsRejected()
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync();
        var original = (await test.Coordinator.OverviewAsync(default)).Configuration;
        Assert.False(original.Enabled); Assert.Null(original.LocalTime);
        Assert.False((await test.Coordinator.ConfigureAsync(original with { Enabled = true }, "admin.ficticio", default)).Succeeded);
        Assert.True((await test.ConfigureAsync()).Succeeded);
        var configured = (await test.Coordinator.OverviewAsync(default)).Configuration;
        Assert.True((await test.Coordinator.ConfigureAsync(configured with { LocalTime = "06:45", EveryDays = 7 }, "admin.ficticio", default)).Succeeded);
        var saved = await test.Coordinator.OverviewAsync(default);
        Assert.Equal("06:45", saved.Configuration.LocalTime); Assert.False(saved.Configuration.Enabled); Assert.Null(saved.NextUtc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedAttemptNeverReplacesLastVerifiedSuccess(bool previousSuccess)
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync(); await test.ConfigureAsync();
        DateTime? validDate = null;
        if (previousSuccess)
        {
            Assert.Equal("Complete", (await test.CopyAsync()).Status);
            validDate = (await test.Coordinator.OverviewAsync(default)).LastValidUtc;
            Assert.NotNull(validDate);
        }
        test.Clock.Now = test.Clock.Now.AddMinutes(1); test.Targets.Connected = false;
        var failure = await test.CopyAsync();
        var view = await test.Coordinator.OverviewAsync(default);
        Assert.Equal("Failed", failure.Status); Assert.Equal(failure.Id, view.LatestAttempt?.Id);
        Assert.Equal(validDate, view.LastValidUtc); Assert.NotNull(view.Alert);
        Assert.False(view.Destination!.Connected);
        using var client = test.App.Browser(); await BrowserForms.LoginAsync(client, "admin.ficticio");
        var html = await client.GetStringAsync("/admin/respaldos");
        Assert.Contains("Último intento:</strong> Fallido", html);
        Assert.Contains(HtmlEncoder.Default.Encode(view.Destination.Message), html);
        Assert.Contains("Últimas ejecuciones", html);
        Assert.Equal(!previousSuccess, html.Contains(HtmlEncoder.Default.Encode("Todavía no hay copias verificadas")));
    }

    [Fact]
    public async Task ConnectionIsVisibleWithoutWriteAuthorizationOrInspectionButton()
    {
        await using var test = new BackupTestContext(authorized: false); await test.InitializeAsync(); await test.ConfigureAsync();
        var view = await test.Coordinator.OverviewAsync(default);
        Assert.True(view.Destination!.Connected); Assert.Equal("Disco ficticio", view.Destination.Disk!.DisplayName);
        using var client = test.App.Browser(); await BrowserForms.LoginAsync(client, "admin.ficticio");
        var html = await client.GetStringAsync("/admin/respaldos");
        Assert.Contains("Disco externo configurado:</strong> Disco ficticio", html);
        test.Targets.Connected = false;
        Assert.Contains("Estado de conexi", await client.GetStringAsync("/admin/respaldos"));
        Assert.False((await test.Coordinator.OverviewAsync(default)).Destination!.Connected);
        Assert.False(Directory.Exists(test.Targets.Root));
    }

    [Fact]
    public async Task InstallationsPersistIndependentSettingsForNewHosts()
    {
        await using var first = new BackupTestContext(); await first.InitializeAsync();
        await using var second = new BackupTestContext(); await second.InitializeAsync();
        second.Targets.VolumeId = "otro-disco-ficticio";
        Assert.True((await first.ConfigureAsync(enabled: true, keep: 3, days: 7)).Succeeded);
        Assert.True((await second.ConfigureAsync(keep: 9, days: 2)).Succeeded);
        var firstSettings = (await first.Coordinator.OverviewAsync(default)).Configuration;
        var secondSettings = (await second.Coordinator.OverviewAsync(default)).Configuration;
        Assert.True((await first.Coordinator.ConfigureAsync(firstSettings with { LocalTime = "06:45" }, "admin.ficticio", default)).Succeeded);
        Assert.True((await second.Coordinator.ConfigureAsync(secondSettings with { LocalTime = "18:10" }, "admin.ficticio", default)).Succeeded);
        var expectedFirst = await first.Coordinator.OverviewAsync(default);
        var expectedSecond = await second.Coordinator.OverviewAsync(default);
        await using var reopenedFirst = new BackupTestContext(root: first.App.Root, preserveData: true);
        await using var reopenedSecond = new BackupTestContext(root: second.App.Root, preserveData: true);
        reopenedSecond.Targets.VolumeId = second.Targets.VolumeId;
        var actualFirst = await reopenedFirst.Coordinator.OverviewAsync(default);
        var actualSecond = await reopenedSecond.Coordinator.OverviewAsync(default);
        Assert.Equal(expectedFirst.Configuration, actualFirst.Configuration);
        Assert.Equal(expectedSecond.Configuration, actualSecond.Configuration);
        Assert.Equal(expectedFirst.NextUtc, actualFirst.NextUtc); Assert.Null(actualSecond.NextUtc);
        Assert.NotEqual(actualFirst.Configuration.VolumeId, actualSecond.Configuration.VolumeId);
        Assert.NotEqual(actualFirst.Configuration.LocalTime, actualSecond.Configuration.LocalTime);
        Assert.True(actualFirst.Configuration.Enabled); Assert.False(actualSecond.Configuration.Enabled);
    }
}
