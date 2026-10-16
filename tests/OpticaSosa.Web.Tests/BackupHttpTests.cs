using System.Net;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Application.Backups;
using OpticaSosa.Application.Security;
using OpticaSosa.Infrastructure.Backups;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class BackupHttpTests
{
    [Theory]
    [InlineData("ventas.ficticio")] [InlineData("profesional.ficticio")]
    public async Task NonAdministratorsCannotUseBackupEndpointsOrApplicationServices(string name)
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync();
        using var client = test.App.Browser(); await BrowserForms.LoginAsync(client, name);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/admin/respaldos")).StatusCode);
        using var scope = test.App.Services.CreateScope();
        using var actor = await ClinicalScenario.OpenAsync(test.App, name);
        var service = new BackupService(test.Coordinator, actor.Actor, test.App.Services.GetRequiredService<ServerBackupFolders>());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.OverviewAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.InspectTargetsAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RequestAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.VerifyAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.BindDestinationAsync(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AdminFormConfiguresWithoutTouchingDiskAndRequiresCsrf(bool externalWritesAuthorized)
    {
        await using var test = new BackupTestContext(authorized: externalWritesAuthorized); await test.InitializeAsync();
        using var client = test.App.Browser(); await BrowserForms.LoginAsync(client, "admin.ficticio");
        var html = await client.GetStringAsync("/admin/respaldos");
        Assert.Contains("Respaldos y recuperación", html); Assert.DoesNotContain("value=\"20:00\"", html);
        Assert.DoesNotContain("<fieldset disabled", html);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/admin/respaldos?handler=Save", new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/admin/respaldos?handler=Bind", new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode);
        var view = await test.Coordinator.OverviewAsync(default);
        var saved = await BrowserForms.PostAsync(client, "/admin/respaldos", "/admin/respaldos?handler=Save", new()
        { ["Enabled"] = "false", ["EveryDays"] = "7", ["LocalTime"] = "07:30", ["TimeZoneId"] = "UTC",
          ["VolumeId"] = FakeBackupTargets.Volume, ["SelectedVolumeId"] = FakeBackupTargets.Volume, ["SelectedRelativeFolder"] = "Respaldos",
          ["KeepCopies"] = "3", ["RelativeFolder"] = "Respaldos", ["Version"] = view.Configuration.Version.ToString() });
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        var stored = await test.Coordinator.OverviewAsync(default);
        Assert.Equal(7, stored.Configuration.EveryDays); Assert.Equal("07:30", stored.Configuration.LocalTime);
        Assert.Equal(3, stored.Configuration.KeepCopies); Assert.Equal(FakeBackupTargets.Volume, stored.Configuration.VolumeId);
        Assert.Equal(externalWritesAuthorized, stored.ExternalWritesAuthorized);
        Assert.False(stored.Configuration.Enabled); Assert.Null(stored.NextUtc);
        Assert.False(Directory.Exists(test.Targets.Root));
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/clinica/profesionales")).StatusCode);
    }

    [Fact]
    public async Task HostedWorkerCompletesQueuedJobWithoutBrowserSession()
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync(); await test.ConfigureAsync();
        Assert.True((await test.Coordinator.RequestAsync("admin.ficticio", default)).Succeeded);
        using var worker = new OpticaSosa.Web.Backups.BackupWorker(test.Coordinator,
            Options.Create(new BackupRuntimeOptions { WorkerEnabled = true }), TimeProvider.System,
            NullLogger<OpticaSosa.Web.Backups.BackupWorker>.Instance);
        await worker.StartAsync(default);
        var until = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < until && !(await test.Coordinator.OverviewAsync(default)).Runs.Any(x => x.Status == "Complete"))
            await Task.Delay(100);
        await worker.StopAsync(default);
        Assert.Equal("Complete", Assert.Single((await test.Coordinator.OverviewAsync(default)).Runs).Status);
    }
}
