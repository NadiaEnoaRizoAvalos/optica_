using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpticaSosa.Application.Customers;
using OpticaSosa.Application.Security;
using OpticaSosa.Application.Viewer;

namespace OpticaSosa.Viewer.Pages;

[Authorize(Policy = Permissions.ReadCustomers)]
public sealed class IndexModel(IViewerReader reader) : PageModel
{
    [BindProperty(SupportsGet = true), StringLength(100)] public string? Search { get; set; }
    public CustomerSearchResult Results { get; private set; } = new([], false);
    public async Task<IActionResult> OnGetAsync()
    {
        if (!ModelState.IsValid) return BadRequest();
        Results = await reader.CustomersAsync(Search, HttpContext.RequestAborted);
        return Page();
    }
}
