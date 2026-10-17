using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Application.Backups;
using OpticaSosa.Infrastructure.Backups;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class BackupOperationalTests
{
    [Theory]
    [InlineData(0, 1, 0, 1)] [InlineData(1, 2, 0, 1)] [InlineData(1, 1, 1, 1)] [InlineData(1, 1, 0, 0)]
    [InlineData(null, null, null, 0)]
    public async Task UnprotectedOrUnverifiableExternalDestinationCannotReceiveAnotherCopy(int? protection, int? conversion, int? locked, int recovery)
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync(); await test.ConfigureAsync();
        Assert.Equal("Complete", (await test.CopyAsync()).Status);
        var previous = Assert.Single(Directory.GetDirectories(Assert.Single(Directory.GetDirectories(test.Targets.Root))));
        var bytes = await File.ReadAllBytesAsync(Path.Combine(previous, "complete.json"));
        test.Protection.Report = test.Protection.Report with { Volumes = [new(FakeBackupTargets.Volume, protection, conversion, locked, recovery)] };
        var failed = await test.CopyAsync(); Assert.Equal("Failed", failed.Status);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(previous, "complete.json")));
        Assert.Single(Directory.GetDirectories(Path.GetDirectoryName(previous)!));
        var view = await test.Coordinator.OverviewAsync(default);
        Assert.NotNull(view.LastValidUtc); Assert.NotNull(view.Alert);
    }

    [Fact]
    public async Task UnknownProviderOrWrongVolumeNeverEstablishesProtection()
    {
        var probe = new FakeBackupProtection(); var guard = new BackupProtectionGuard(probe);
        await Assert.ThrowsAsync<InvalidDataException>(() => guard.DemandAsync("otro-volumen-ficticio", default));
        probe.Report = probe.Report with { ProviderAvailable = false };
        await Assert.ThrowsAsync<InvalidDataException>(() => guard.DemandAsync(FakeBackupTargets.Volume, default));
    }

    [Theory]
    [InlineData("ventas.ficticio")] [InlineData("profesional.ficticio")]
    public async Task ProtectionQueryRetainsAdministratorOnlyAuthorization(string name)
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync();
        using var actor = await ClinicalScenario.OpenAsync(test.App, name);
        var service = new BackupProtectionService(test.Protection, actor.Actor);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.InspectAsync());
        using var client = test.App.Browser(); await BrowserForms.LoginAsync(client, name);
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/admin/respaldos?handler=Protection", new StringContent(""))).StatusCode);
    }

    [Fact]
    public async Task AdminCanInspectProtectionWithoutChangingConfigurationOrObtainingClinicalAccess()
    {
        await using var test = new BackupTestContext(authorized: false); await test.InitializeAsync();
        Assert.True((await test.ConfigureAsync()).Succeeded);
        using var client = test.App.Browser(); await BrowserForms.LoginAsync(client, "admin.ficticio");
        var before = (await test.Coordinator.OverviewAsync(default)).Configuration;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/admin/respaldos?handler=Protection", new StringContent(""))).StatusCode);
        var result = await BrowserForms.PostAsync(client, "/admin/respaldos", "/admin/respaldos?handler=Protection", new());
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var html = WebUtility.HtmlDecode(await result.Content.ReadAsStringAsync());
        Assert.Contains("Windows ficticio", html); Assert.Contains("Running", html);
        Assert.Contains("no demuestra", html);
        Assert.Contains("Cifrado completo, protección activa", html);
        Assert.Equal(before, (await test.Coordinator.OverviewAsync(default)).Configuration);
        Assert.False(Directory.Exists(test.Targets.Root));
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/clinica/profesionales")).StatusCode);
    }
}
