using System.Text;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Application.Clinical;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class PrivateAttachmentStoreTests
{
    [Fact]
    public async Task AtomicStorePreservesExistingFilesAndCleansItsOwnTemporaryFileAfterFailures()
    {
        await using var app = new SecurityApp();
        using var scope = app.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IPrivateAttachmentStore>();
        var id = Guid.NewGuid(); var bytes = Encoding.UTF8.GetBytes("Contenido ficticio");
        await store.WriteAsync(id, bytes);
        Assert.Equal(bytes, await store.ReadAsync(id));
        await Assert.ThrowsAsync<IOException>(() => store.WriteAsync(id, Encoding.UTF8.GetBytes("No sobrescribir")));
        Assert.Equal(bytes, await store.ReadAsync(id));
        Assert.Null(await store.ReadAsync(Guid.NewGuid()));
        Assert.Empty(Directory.GetFiles(Path.Combine(app.Root, "private", "attachments", "clinical"), "*.pending"));
        await Assert.ThrowsAsync<ArgumentException>(() => store.WriteAsync(Guid.Empty, bytes));
    }

    [Fact]
    public async Task CancelledWriteDoesNotPublishPartialFilesOrLeaveItsTemporaryFile()
    {
        await using var app = new SecurityApp();
        using var scope = app.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IPrivateAttachmentStore>();
        var id = Guid.NewGuid();
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.WriteAsync(id, new byte[81920], cancelled.Token));
        Assert.Null(await store.ReadAsync(id));
        Assert.Empty(Directory.GetFiles(Path.Combine(app.Root, "private", "attachments", "clinical")));
    }
}
