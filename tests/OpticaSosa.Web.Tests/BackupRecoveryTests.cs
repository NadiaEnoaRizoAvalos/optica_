using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Domain.Backups;
using OpticaSosa.Domain.Work;
using OpticaSosa.Infrastructure.Backups;
using OpticaSosa.Infrastructure.Clinical;
using OpticaSosa.Infrastructure.Persistence;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class BackupRecoveryTests
{
    [Fact]
    public async Task AutomaticBackupPreservesClinicalDocumentsAndExistingQualityControls()
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync();
        using var pro = await ClinicalScenario.OpenAsync(test.App);
        using var customers = await CustomerScenario.OpenAsync(test.App);
        var customer = (await customers.Customers.CreateAsync(CustomerScenario.Input())).CustomerId!.Value;
        var record = await pro.Clinical.CreateAsync(customer, ClinicalScenario.Input(await pro.AddProfessionalAsync()));
        var detail = (await pro.Clinical.GetAsync(customer, record.Id!.Value))!;
        var bytes = BusinessForms.Logo(); using var input = new MemoryStream(bytes);
        var upload = await pro.Attachments.UploadAsync(customer, record.Id.Value, new(Guid.NewGuid(), detail.RevisionId, detail.Version,
            "receta-ficticia.png", "image/png", bytes.Length, input)); Assert.True(upload.Succeeded);
        var work = QualityWorkflowTests.Service(pro);
        var createdWork = await work.CreateAsync(new() { CustomerId = customer, Product = "Anteojos ficticios", Frame = "Armazón ficticio" });
        Assert.True(createdWork.Succeeded, createdWork.Error);
        var id = createdWork.Id!.Value; await QualityWorkflowTests.PrepareAsync(work, id);
        Assert.True((await work.SaveControlAsync(id, await QualityWorkflowTests.InputAsync(work, id, QualityStage.Technical))).Succeeded);
        await test.ConfigureAsync(); await test.CopyAsync();
        var directory = Assert.Single(Directory.GetDirectories(Assert.Single(Directory.GetDirectories(test.Targets.Root))));
        var restoredPath = await test.App.Services.GetRequiredService<BackupVerifier>().RestoreIsolatedAsync(directory);
        var options = new DbContextOptionsBuilder<OpticaDbContext>().UseSqlite($"Data Source={Path.Combine(restoredPath, "optica.db")};Mode=ReadOnly;Pooling=False").Options;
        await using var restored = new OpticaDbContext(options);
        Assert.Equal(await pro.Db.ClinicalRevisions.CountAsync(), await restored.ClinicalRevisions.CountAsync());
        Assert.Equal(await pro.Db.Professionals.CountAsync(), await restored.Professionals.CountAsync());
        Assert.Equal(1, await restored.QualityControls.CountAsync()); Assert.Equal(WorkStatus.PendingControl, (await restored.OpticalWorks.SingleAsync()).Status);
        var file = await restored.ClinicalAttachments.SingleAsync();
        Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(restoredPath, "attachments", "clinical", file.Id.ToString("N") + ".bin")));
    }

    [Fact]
    public async Task CompleteManifestAfterCrashIsReconciledWithoutAnotherCopy()
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync(); await test.ConfigureAsync();
        await using var db = await test.App.Services.GetRequiredService<IDbContextFactory<OpticaDbContext>>().CreateDbContextAsync();
        var settings = await db.BackupSettings.SingleAsync();
        var run = new BackupRun { Status = "Running", StartedUtc = test.Clock.Now.UtcDateTime,
            VolumeId = FakeBackupTargets.Volume, RelativeFolder = settings.RelativeFolder, BindingId = settings.BindingId };
        run.DirectoryName = run.Id.ToString("N"); db.Add(run); await db.SaveChangesAsync();
        var target = test.Targets.Resolve(FakeBackupTargets.Volume, settings.InstallationId, true, settings.RelativeFolder, settings.BindingId);
        await test.App.Services.GetRequiredService<ClinicalSnapshotWriter>().CreateAsync(target.Directory, default, run.Id);
        var actualCompletion = test.Clock.Now.UtcDateTime;
        test.Clock.Now = test.Clock.Now.AddHours(3);
        await test.Coordinator.TickAsync();
        Assert.Equal("Complete", Assert.Single((await test.Coordinator.OverviewAsync(default)).Runs).Status);
        Assert.Equal(actualCompletion, (await test.Coordinator.OverviewAsync(default)).LastValidUtc);
        Assert.Single(Directory.GetDirectories(target.Directory));
    }

    [Fact]
    public async Task ScheduleCanBeDisabledWhileDestinationIsDisconnected()
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync(); await test.ConfigureAsync(enabled: true);
        test.Targets.Connected = false;
        Assert.True((await test.ConfigureAsync(enabled: false)).Succeeded);
        Assert.False((await test.Coordinator.OverviewAsync(default)).Configuration.Enabled);
    }

    [Theory]
    [InlineData("{\"Status\":\"Complete\"}")] [InlineData("texto ficticio no JSON")]
    public async Task InvalidManifestIsRejectedWithoutUnhandledParserErrors(string contents)
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync(); await test.ConfigureAsync(); var run = await test.CopyAsync();
        var directory = Assert.Single(Directory.GetDirectories(Assert.Single(Directory.GetDirectories(test.Targets.Root))));
        await File.WriteAllTextAsync(Path.Combine(directory, "complete.json"), contents);
        Assert.False((await test.Coordinator.VerifyAsync(run.Id, "admin.ficticio", default)).Succeeded);
        var view = await test.Coordinator.OverviewAsync(default);
        Assert.Null(view.LastValidUtc); Assert.Equal("Corrupt", Assert.Single(view.Runs).Status);
    }

    [Fact]
    public async Task SourceCapacityFailureLeavesPreviousCopiesUntouched()
    {
        await using var test = new BackupTestContext(); await test.InitializeAsync(); await test.ConfigureAsync(); await test.CopyAsync();
        var paths = test.App.Services.GetRequiredService<StoragePaths>();
        var verifier = new BackupVerifier(paths, new NoCapacity(), Microsoft.Extensions.Options.Options.Create(new BackupRuntimeOptions()));
        var directory = Assert.Single(Directory.GetDirectories(Assert.Single(Directory.GetDirectories(test.Targets.Root))));
        await Assert.ThrowsAsync<IOException>(() => verifier.RestoreIsolatedAsync(directory));
        Assert.True(File.Exists(Path.Combine(directory, "complete.json")));
    }
    private sealed class NoCapacity : IStorageCapacity { public long AvailableBytes => 0; }
}
