namespace OpticaSosa.Infrastructure.Identity;

public sealed class AuditEntry
{
    public long Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public string Action { get; set; } = string.Empty;
    public string? ActorId { get; set; }
    public string? TargetId { get; set; }
}
