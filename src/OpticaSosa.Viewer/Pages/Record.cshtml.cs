using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Security;
using OpticaSosa.Application.Viewer;

namespace OpticaSosa.Viewer.Pages;

[Authorize(Policy = Permissions.ReadClinical)]
public sealed class RecordModel(IViewerReader reader) : PageModel
{
    public ClinicalDetails Record { get; private set; } = null!;
    public AttachmentPage Documents { get; private set; } = new([], 1, false);
    public async Task<IActionResult> OnGetAsync(Guid customer, Guid id, int? revision, int page = 1, int documentPage = 1)
    {
        if (!HistoryPaging.IsValid(page) || !HistoryPaging.IsValid(documentPage) || revision is <= 0) return BadRequest();
        var record = await reader.RecordAsync(customer, id, revision, page, HttpContext.RequestAborted);
        if (record is null) return NotFound();
        Record = record;
        Documents = await reader.DocumentsAsync(customer, id, record.RevisionId, documentPage, HttpContext.RequestAborted);
        return Page();
    }
}
