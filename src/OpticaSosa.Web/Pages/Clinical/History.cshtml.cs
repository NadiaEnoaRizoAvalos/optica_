using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Customers;
using OpticaSosa.Application.Security;

namespace OpticaSosa.Web.Pages.Clinical;

[Authorize(Policy = Permissions.ReadClinical)]
public sealed class HistoryModel(IClinicalService clinical, ICustomerService customers) : PageModel
{
    public CustomerSummary Customer { get; private set; } = null!;
    public ClinicalHistory History { get; private set; } = new([], 1, false);
    public async Task<IActionResult> OnGetAsync(Guid customerId, int pagina = 1)
    {
        if (pagina < 1 || pagina > 100000) return BadRequest();
        var details = await customers.GetAsync(customerId, ct: HttpContext.RequestAborted);
        if (details is null) return NotFound();
        Customer = details.Customer;
        History = await clinical.ListAsync(customerId, pagina, HttpContext.RequestAborted);
        return Page();
    }
}
