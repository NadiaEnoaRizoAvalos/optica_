using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Customers;
using OpticaSosa.Application.Security;
namespace OpticaSosa.Web.Pages.Clinical;
[Authorize(Policy = Permissions.ReadClinical)]
public sealed class DirectoryModel(ICustomerService customers) : PageModel
{
    [BindProperty, System.ComponentModel.DataAnnotations.StringLength(100)] public string? Search { get; set; }
    public CustomerSearchResult Results { get; private set; } = new([], false);
    public void OnGet() { }
    public async Task OnPostAsync() { if (ModelState.IsValid) Results = await customers.SearchAsync(Search, ct: HttpContext.RequestAborted); }
}
