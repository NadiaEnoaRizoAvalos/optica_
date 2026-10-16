using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Security;

namespace OpticaSosa.Web.Pages.Clinical;

[Authorize(Policy = Permissions.ReadClinical)]
public sealed class DownloadModel(IClinicalAttachmentService attachments) : PageModel
{
    public async Task<IActionResult> OnGetAsync(Guid customerId, Guid id, Guid attachmentId)
    {
        try
        {
            var file = await attachments.DownloadAsync(customerId, id, attachmentId, HttpContext.RequestAborted);
            if (file is null) return NotFound();
            Response.Headers.ContentSecurityPolicy = "sandbox; default-src 'none'";
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (InvalidDataException ex) { return new ContentResult { StatusCode = 409, Content = ex.Message, ContentType = "text/plain; charset=utf-8" }; }
    }
}
