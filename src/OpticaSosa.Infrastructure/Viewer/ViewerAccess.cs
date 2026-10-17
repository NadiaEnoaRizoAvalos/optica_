using System.Collections.Concurrent;
using System.Text.Json;
using OpticaSosa.Infrastructure.Viewer;

namespace OpticaSosa.Infrastructure.Viewer;

public sealed class ViewerAccess(ViewerSnapshot snapshot, ViewerJournal journal, TimeProvider clock) : IDisposable
{
    private readonly ConcurrentDictionary<string, byte> revoked = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> failures = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> lockedUntil = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim gate = new(1, 1);
    public bool IsRevoked(string id) => revoked.ContainsKey(id);
    public bool IsLocked(string id) => lockedUntil.TryGetValue(id, out var until) && until > clock.GetUtcNow();
    public void Failed(string id)
    {
        if (failures.AddOrUpdate(id, 1, (_, previous) => previous + 1) >= 5)
        { lockedUntil[id] = clock.GetUtcNow().AddMinutes(15); failures.TryRemove(id, out _); }
    }
    public void Succeeded(string id) => failures.TryRemove(id, out _);
    internal async Task RevokeAsync(string actor, string target, CancellationToken ct)
    {
        if (actor == target) throw new InvalidOperationException("No podés revocar tu propio acceso administrativo.");
        await gate.WaitAsync(ct);
        try
        {
            if (IsRevoked(actor)) throw new UnauthorizedAccessException();
            if (IsRevoked(target)) return;
            await journal.RecordAsync("viewer.access.revoked", actor, target, ct);
            await File.AppendAllTextAsync(Path.Combine(snapshot.Workspace, "revocations.jsonl"), JsonSerializer.Serialize(target) + "\n", ct);
            revoked[target] = 0;
        }
        finally { gate.Release(); }
    }
    public void Dispose() => gate.Dispose();
}
