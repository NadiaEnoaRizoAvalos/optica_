using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Customers;
using OpticaSosa.Application.Work;
using OpticaSosa.Application.Security;

namespace OpticaSosa.Application.Viewer;

// El visor no publica contratos de escritura ni una segunda aplicación operativa.
public interface IViewerReader
{
    Task<CustomerSearchResult> CustomersAsync(string? query, CancellationToken ct = default);
    Task<CustomerDetails?> CustomerAsync(Guid id, int page, CancellationToken ct = default);
    Task<ClinicalHistory> HistoryAsync(Guid customer, int page, CancellationToken ct = default);
    Task<ClinicalDetails?> RecordAsync(Guid customer, Guid id, int? revision, int page, CancellationToken ct = default);
    Task<AttachmentPage> DocumentsAsync(Guid customer, Guid record, Guid revision, int page, CancellationToken ct = default);
    Task<AttachmentDownload?> DocumentAsync(Guid customer, Guid record, Guid id, CancellationToken ct = default);
    Task<WorkPage> WorksAsync(Guid? customer, int page, CancellationToken ct = default);
    Task<WorkDetails?> WorkAsync(Guid id, int page, CancellationToken ct = default);
}

public interface IViewerAudit
{
    Task RecordAsync(string action, string? actor, string? target = null, CancellationToken ct = default);
}

public sealed record ViewerAccount(AccountSummary Account, bool IsRevoked);
public interface IViewerAccounts
{
    Task<IReadOnlyList<ViewerAccount>> ListAsync(CancellationToken ct = default);
    Task<AccountResult> RevokeAsync(string? target, bool confirmed, CancellationToken ct = default);
}
