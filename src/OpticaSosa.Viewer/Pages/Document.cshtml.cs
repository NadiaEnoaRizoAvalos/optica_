using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Net.Http.Headers;
using OpticaSosa.Application.Security;
using OpticaSosa.Application.Viewer;

namespace OpticaSosa.Viewer.Pages;

[Authorize(Policy = Permissions.ReadClinical)]
public sealed class DocumentModel(IViewerReader reader) : PageModel
{
    public async Task<IActionResult> OnGetAsync(Guid customer, Guid record, Guid id)
    {
        var document = await reader.DocumentAsync(customer, record, id, HttpContext.RequestAborted);
        if (document is null) return NotFound();
        Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline") { FileNameStar = document.FileName }.ToString();
        return File(document.Content, document.ContentType);
    }
}
