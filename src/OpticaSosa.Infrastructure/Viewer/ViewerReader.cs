using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Customers;
using OpticaSosa.Application.Work;
using OpticaSosa.Application.Viewer;
using OpticaSosa.Application.Security;
using OpticaSosa.Infrastructure.Clinical;
using OpticaSosa.Infrastructure.Customers;
using OpticaSosa.Infrastructure.Work;

namespace OpticaSosa.Infrastructure.Viewer;

// Reutiliza consultas, contratos, paginación, filtros por rol y controles clínicos.
// Los comandos de los servicios originales no se publican; SQLite y ReadGuard
// rechazan escrituras incluso si se invocan por error desde otro componente.
public sealed class ViewerReader(CustomerService customers, ClinicalService clinical,
    ClinicalAttachmentService documents, WorkService works, IViewerAudit audit, ICurrentUser current) : IViewerReader
{
    public Task<CustomerSearchResult> CustomersAsync(string? query, CancellationToken ct = default) => customers.SearchAsync(query, true, ct);
    public Task<CustomerDetails?> CustomerAsync(Guid id, int page, CancellationToken ct = default) => customers.GetAsync(id, page, ct);
    public Task<ClinicalHistory> HistoryAsync(Guid customer, int page, CancellationToken ct = default) => clinical.ListAsync(customer, page, ct);
    public Task<ClinicalDetails?> RecordAsync(Guid customer, Guid id, int? revision, int page, CancellationToken ct = default) => clinical.GetAsync(customer, id, revision, page, ct);
    public Task<AttachmentPage> DocumentsAsync(Guid customer, Guid record, Guid revision, int page, CancellationToken ct = default) => documents.ListAsync(customer, record, revision, page, ct);
    public async Task<AttachmentDownload?> DocumentAsync(Guid customer, Guid record, Guid id, CancellationToken ct = default)
    {
        var document = await documents.DownloadAsync(customer, record, id, ct);
        if (document is null) return null;
        try
        {
            var expected = ClinicalFileValidation.ExpectedType(document.FileName);
            if (!string.Equals(document.ContentType, expected, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Tipo y extensión no coinciden.");
            return document;
        }
        catch (ArgumentException)
        {
            await document.Content.DisposeAsync();
            await audit.RecordAsync("viewer.document.type-rejected", current.Id, id.ToString(), ct);
            throw new NotSupportedException("Formato no permitido en el visor. El documento original se conserva.");
        }
    }
    public Task<WorkPage> WorksAsync(Guid? customer, int page, CancellationToken ct = default) => works.ListAsync(customer, page: page, ct: ct);
    public Task<WorkDetails?> WorkAsync(Guid id, int page, CancellationToken ct = default) => works.GetAsync(id, page, ct);
}
