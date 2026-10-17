using System.Text.Json;
using OpticaSosa.Application.Security;
using OpticaSosa.Application.Viewer;
using OpticaSosa.Infrastructure.Viewer;

namespace OpticaSosa.Infrastructure.Viewer;

public sealed class ViewerJournal(ViewerSnapshot snapshot, TimeProvider clock) : IViewerAudit, ISecurityAudit, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public async Task RecordAsync(string action, string? actor, string? target = null, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try
        {
            var line = JsonSerializer.Serialize(new AuditSummary(clock.GetUtcNow(), action, actor, target));
            await File.AppendAllTextAsync(Path.Combine(snapshot.Workspace, "viewer-audit.jsonl"), line + "\n", ct);
        }
        finally { gate.Release(); }
    }
    public void Dispose() => gate.Dispose();
}
