using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Application.Commerce;
using OpticaSosa.Infrastructure.Persistence;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class SalesRecoveryTests
{
    [Fact]
    public async Task ExistingSnapshotRecoversOrdersPaymentsHistoryAndClinicalRelationshipsWithoutMigrating()
    {
        await using var test = await TechnicalTestContext.CreateAsync(); using var catalog = await CatalogScenario.OpenAsync(test.App);
        using var admin = await CommerceScenario.OpenAsync(test.App);
        var item = await SalesOrderTests.VariantAsync(catalog);
        var publication = await test.Publications.PublishAsync(test.CustomerId, test.RecordId, await test.InputAsync()); Assert.True(publication.Succeeded);
        var id = await admin.ConfirmAsync(await admin.DraftAsync(catalog, test.CustomerId, item.Variant));
        var before = (await admin.Orders.GetAsync(id))!; var paymentId = Guid.NewGuid();
        Assert.True((await admin.Payments.ReceiveAsync(id, new(paymentId, before.Version, 25m, "ARS", "Efectivo ficticio"))).Succeeded);
        using var snapshotActor = await ClinicalScenario.OpenAsync(test.App, "admin.ficticio");
        var snapshot = await snapshotActor.Snapshots.CreateAsync(); Assert.Equal("Complete", snapshot.Status);
        var backup = Path.Combine(snapshot.Directory, "optica.db"); var hash = SHA256.HashData(await File.ReadAllBytesAsync(backup));
        await using var restored = new SecurityApp();
        using (var scope = restored.Services.CreateScope())
        {
            var paths = scope.ServiceProvider.GetRequiredService<StoragePaths>();
            Assert.Equal(Path.Combine(restored.Root, "private"), paths.DataDirectory);
            File.Copy(backup, paths.DatabaseFile, overwrite: false);
        }
        using var recovered = await CommerceScenario.OpenAsync(restored);
        var order = (await recovered.Orders.GetAsync(id))!;
        Assert.Equal(before.CustomerId, order.CustomerId); Assert.Equal(10000, order.TotalMinor); Assert.Equal(2500, order.NetPaidMinor);
        Assert.Equal(item.Variant, Assert.Single((await recovered.Orders.LinesAsync(id))!.Items).VariantId);
        Assert.Equal(paymentId, Assert.Single((await recovered.Payments.ListAsync(id))!.Items).Id);
        using var clinical = await ClinicalScenario.OpenAsync(restored);
        Assert.Equal(1.125m, (await clinical.Clinical.GetAsync(test.CustomerId, test.RecordId))!.Data.Right.Sphere);
        var technical = new OpticaSosa.Infrastructure.Technical.TechnicalPublicationService(clinical.Factory, clinical.Actor, TimeProvider.System);
        Assert.NotNull(await technical.GetAsync(test.CustomerId, publication.Id!.Value));
        Assert.Equal(hash, SHA256.HashData(await File.ReadAllBytesAsync(backup)));
        Assert.Equal(order.Version, (await admin.Orders.GetAsync(id))!.Version);
    }
}
