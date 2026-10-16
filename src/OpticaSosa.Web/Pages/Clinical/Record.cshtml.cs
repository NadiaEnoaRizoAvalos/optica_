using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Security;

namespace OpticaSosa.Web.Pages.Clinical;

[Authorize(Policy = Permissions.ReadClinical)]
public sealed class RecordModel(IClinicalService clinical) : PageModel
{
    public ClinicalDetails Details { get; private set; } = null!;
    public async Task<IActionResult> OnGetAsync(Guid customerId, Guid id, int? revision, int pagina = 1)
    {
        if (pagina < 1 || pagina > 100000 || revision is < 1) return BadRequest();
        var details = await clinical.GetAsync(customerId, id, revision, pagina, HttpContext.RequestAborted);
        if (details is null) return NotFound();
        Details = details;
        return Page();
    }
}
