using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Security;
using OpticaSosa.Application.Viewer;
using OpticaSosa.Application.Work;

namespace OpticaSosa.Viewer.Pages;

[Authorize(Policy = Permissions.ReadOrders)]
public sealed class WorksModel(IViewerReader reader) : PageModel
{
    public WorkPage Results { get; private set; } = new([], 1, false);
    public async Task<IActionResult> OnGetAsync(int page = 1)
    {
        if (!HistoryPaging.IsValid(page)) return BadRequest();
        Results = await reader.WorksAsync(null, page, HttpContext.RequestAborted); return Page();
    }
}
