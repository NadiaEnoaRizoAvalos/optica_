namespace OpticaSosa.Application.Security;

public sealed record AccountSummary(string Id, string UserName, bool IsActive, IReadOnlyList<string> Roles, string ConcurrencyStamp);
public sealed record AccountResult(bool Succeeded, string? Error = null);
public sealed record AuditSummary(DateTimeOffset OccurredAt, string Action, string? ActorId, string? TargetId);

public interface IAccountService
{
    Task<IReadOnlyList<AccountSummary>> ListAsync(CancellationToken cancellationToken = default);
    Task<AccountResult> CreateAsync(string userName, string password, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);
    Task<AccountResult> ChangeRolesAsync(string userId, string concurrencyStamp, IReadOnlyCollection<string> roles,
        bool confirmed, CancellationToken cancellationToken = default);
    Task<AccountResult> DeactivateAsync(string userId, CancellationToken cancellationToken = default);
    Task<AccountResult> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditSummary>> ReadAuditAsync(CancellationToken cancellationToken = default);
}

public interface ISecurityAudit
{
    Task RecordAsync(string action, string? actorId, string? targetId = null, CancellationToken cancellationToken = default);
}
