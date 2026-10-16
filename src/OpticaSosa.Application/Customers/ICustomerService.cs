using OpticaSosa.Domain.Customers;

namespace OpticaSosa.Application.Customers;

public sealed record CustomerSummary(Guid Id, string FirstName, string LastName, string Document, string? Phone,
    bool IsActive, bool IsRecurrent);
public sealed record CustomerSearchResult(IReadOnlyList<CustomerSummary> Items, bool HasMore);
public sealed record VisitSummary(Guid Id, string Reason, string? AdministrativeNotes, DateTime StartedAtUtc,
    DateTime? EndedAtUtc, VisitStatus Status, bool WasRecurrent, Guid Version, string RecordedByUserId = "", string? EndedByUserId = null);
public sealed record CustomerDetails(CustomerSummary Customer, string? Email, Guid Version,
    IReadOnlyList<VisitSummary> Visits, int HistoryPage, bool HasOlderVisits, DateTime CreatedAtUtc = default, int VisitCount = 0, DateTime? LastVisitUtc = null);
public enum CustomerResultCode { Success, Invalid, DuplicateDocument, PossibleDuplicate, Conflict, NotFound }
public sealed record CustomerResult(CustomerResultCode Code, Guid? CustomerId = null, string? Error = null,
    IReadOnlyList<CustomerSummary>? PossibleDuplicates = null)
{
    public bool Succeeded => Code == CustomerResultCode.Success;
}

public interface ICustomerService
{
    Task<CustomerSearchResult> SearchAsync(string? query, bool includeInactive = false, CancellationToken ct = default);
    Task<CustomerDetails?> GetAsync(Guid customerId, int historyPage = 1, CancellationToken ct = default);
    Task<CustomerResult> CreateAsync(CustomerInput input, CancellationToken ct = default);
    Task<CustomerResult> UpdateAsync(Guid customerId, CustomerInput input, CancellationToken ct = default);
    Task<CustomerResult> DeactivateAsync(Guid customerId, Guid version, CancellationToken ct = default);
}
