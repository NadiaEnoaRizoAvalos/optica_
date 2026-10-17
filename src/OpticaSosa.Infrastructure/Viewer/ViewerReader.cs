using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Customers;
using OpticaSosa.Application.Work;
using OpticaSosa.Application.Viewer;
using OpticaSosa.Infrastructure.Clinical;
using OpticaSosa.Infrastructure.Customers;
using OpticaSosa.Infrastructure.Work;

namespace OpticaSosa.Infrastructure.Viewer;

// Reutiliza consultas, contratos, paginación, filtros por rol y controles clínicos.
// Los comandos de los servicios originales no se publican; SQLite y ReadGuard
// rechazan escrituras incluso si se invocan por error desde otro componente.
public sealed class ViewerReader(CustomerService customers, ClinicalService clinical,
    ClinicalAttachmentService documents, WorkService works) : IViewerReader
{
    public Task<CustomerSearchResult> CustomersAsync(string? query, CancellationToken ct = default) => customers.SearchAsync(query, true, ct);
    public Task<CustomerDetails?> CustomerAsync(Guid id, int page, CancellationToken ct = default) => customers.GetAsync(id, page, ct);
    public Task<ClinicalHistory> HistoryAsync(Guid customer, int page, CancellationToken ct = default) => clinical.ListAsync(customer, page, ct);
    public Task<ClinicalDetails?> RecordAsync(Guid customer, Guid id, int? revision, int page, CancellationToken ct = default) => clinical.GetAsync(customer, id, revision, page, ct);
    public Task<AttachmentPage> DocumentsAsync(Guid customer, Guid record, Guid revision, int page, CancellationToken ct = default) => documents.ListAsync(customer, record, revision, page, ct);
    public Task<AttachmentDownload?> DocumentAsync(Guid customer, Guid record, Guid id, CancellationToken ct = default) => documents.DownloadAsync(customer, record, id, ct);
    public Task<WorkPage> WorksAsync(Guid? customer, int page, CancellationToken ct = default) => works.ListAsync(customer, page: page, ct: ct);
    public Task<WorkDetails?> WorkAsync(Guid id, int page, CancellationToken ct = default) => works.GetAsync(id, page, ct);
}
