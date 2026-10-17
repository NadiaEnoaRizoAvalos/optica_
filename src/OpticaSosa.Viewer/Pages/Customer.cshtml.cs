using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Customers;
using OpticaSosa.Application.Security;
using OpticaSosa.Application.Viewer;
using OpticaSosa.Application.Work;

namespace OpticaSosa.Viewer.Pages;

[Authorize(Policy = Permissions.ReadCustomers)]
public sealed class CustomerModel(IViewerReader reader, ICurrentUser current) : PageModel
{
    public CustomerDetails Details { get; private set; } = null!;
    public ClinicalHistory? History { get; private set; }
    public WorkPage? Works { get; private set; }
    public async Task<IActionResult> OnGetAsync(Guid id, int visitsPage = 1, int clinicalPage = 1, int workPage = 1)
    {
        if (!HistoryPaging.IsValid(visitsPage) || !HistoryPaging.IsValid(clinicalPage) || !HistoryPaging.IsValid(workPage)) return BadRequest();
        var result = await reader.CustomerAsync(id, visitsPage, HttpContext.RequestAborted);
        if (result is null) return NotFound();
        Details = result;
        if (current.HasPermission(Permissions.ReadClinical)) History = await reader.HistoryAsync(id, clinicalPage, HttpContext.RequestAborted);
        if (current.HasPermission(Permissions.ReadOrders)) Works = await reader.WorksAsync(id, workPage, HttpContext.RequestAborted);
        return Page();
    }
}
