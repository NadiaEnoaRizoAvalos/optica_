using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Infrastructure.Inventory;
using OpticaSosa.Infrastructure.Laboratory;
using OpticaSosa.Infrastructure.Persistence;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class LaboratoryRecoveryTests
{
    [Fact]
    public async Task ExistingBackupRestoresLaboratoryReceiptsOriginalTechnicalVersionStockAndChangesTogether()
    {
        await using var test = await LaboratoryScenario.CreateAsync(); var inventory = test.Inventory; var labId = await test.AuthorizeAsync();
        await test.DispatchAsync(labId);
        Assert.True((await test.Laboratory.ReceiveAsync(inventory.OrderId, labId, await test.ReceiptAsync(labId, 2, 1))).Succeeded);
        var lab = (await test.Laboratory.GetAsync(inventory.OrderId, labId))!;
        Assert.True((await test.Laboratory.CancelAsync(inventory.OrderId, labId, new(Guid.NewGuid(), lab.OrderVersion, lab.Version, 1,
            "Cancelación ficticia de resto", "Confirmación ficticia"))).Succeeded);
        using var backupActor = await ClinicalScenario.OpenAsync(inventory.Test.App, "admin.ficticio");
        var backup = await backupActor.Snapshots.CreateAsync(); Assert.Equal("Complete", backup.Status);
        var file = Path.Combine(backup.Directory, "optica.db"); var hash = SHA256.HashData(await File.ReadAllBytesAsync(file));
        await using var restored = new SecurityApp();
        using (var scope = restored.Services.CreateScope())
        {
            var paths = scope.ServiceProvider.GetRequiredService<StoragePaths>();
            Assert.Equal(Path.Combine(restored.Root, "private"), paths.DataDirectory);
            File.Copy(file, paths.DatabaseFile, overwrite: false);
        }
        using var account = await CommerceScenario.OpenAsync(restored);
        var service = new LaboratoryService(account.Factory, account.Actor, inventory.Clock);
        var recovered = (await service.GetAsync(inventory.OrderId, labId))!;
        Assert.Equal(2, recovered.Received); Assert.Equal(1, recovered.Quarantined); Assert.Equal(1, recovered.Cancelled);
        Assert.Equal(test.AssignmentId, recovered.TechnicalAssignmentId); Assert.Single((await service.ReceiptsAsync(inventory.OrderId, labId))!.Items);
        Assert.Single((await service.ChangesAsync(inventory.OrderId, labId))!.Items);
        var stock = Assert.Single((await new StockService(account.Factory, account.Actor, inventory.Clock).BatchesAsync()).Items);
        Assert.Equal(2, stock.OnHand); Assert.Equal(1, stock.Quarantined);
        Assert.Equal(hash, SHA256.HashData(await File.ReadAllBytesAsync(file)));
        Assert.Equal(2, (await test.Laboratory.GetAsync(inventory.OrderId, labId))!.Received);
    }
}
