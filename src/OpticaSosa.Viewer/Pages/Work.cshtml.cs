using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Security;
using OpticaSosa.Application.Viewer;
using OpticaSosa.Application.Work;

namespace OpticaSosa.Viewer.Pages;

[Authorize(Policy = Permissions.ReadOrders)]
public sealed class WorkModel(IViewerReader reader) : PageModel
{
    public WorkDetails Details { get; private set; } = null!;
    public async Task<IActionResult> OnGetAsync(Guid id, int page = 1)
    {
        if (!HistoryPaging.IsValid(page)) return BadRequest();
        var result = await reader.WorkAsync(id, page, HttpContext.RequestAborted);
        if (result is null) return NotFound();
        Details = result; return Page();
    }
}
