using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Domain.Backups;
using OpticaSosa.Infrastructure.Persistence;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class BackupCompletionTests
{
    [Fact]
    public async Task ShutdownAfterVerifiedCommitKeepsSuccessAndReportsInterruptedRetention()
    {
        using var stopping = new CancellationTokenSource();
        var interceptor = new CancelAfterCompletion(stopping);
        await using var test = new BackupTestContext(configureServices: services =>
            services.AddDbContextFactory<OpticaDbContext>(options => options.AddInterceptors(interceptor)));
        await test.InitializeAsync(); Assert.True((await test.ConfigureAsync()).Succeeded);
        Assert.True((await test.Coordinator.RequestAsync("admin.ficticio", default)).Succeeded);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => test.Coordinator.TickAsync(stopping.Token));
        var view = await test.Coordinator.OverviewAsync(default);
        var run = Assert.Single(view.Runs);
        Assert.True(stopping.IsCancellationRequested);
        Assert.Equal("Complete", run.Status); Assert.NotNull(view.LastValidUtc);
        Assert.Contains("retención", run.Error!); Assert.NotNull(view.Alert);
        Assert.True((await test.Coordinator.VerifyAsync(run.Id, "admin.ficticio", default)).Succeeded);
        await test.Coordinator.TickAsync();
        Assert.Single((await test.Coordinator.OverviewAsync(default)).Runs);
    }

    private sealed class CancelAfterCompletion(CancellationTokenSource stopping) : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData,
            int result, CancellationToken cancellationToken = default)
        {
            if (cancellationToken == stopping.Token && eventData.Context!.ChangeTracker.Entries<BackupRun>()
                .Any(x => x.Entity.Status == "Complete")) stopping.Cancel();
            return ValueTask.FromResult(result);
        }
    }
}
