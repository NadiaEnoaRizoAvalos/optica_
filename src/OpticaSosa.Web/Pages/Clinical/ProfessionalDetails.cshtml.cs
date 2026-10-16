using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Security;
namespace OpticaSosa.Web.Pages.Clinical;
[Authorize(Policy = Permissions.ReadClinical)]
public sealed class ProfessionalDetailsModel(IProfessionalService professionals, IClinicalDirectory directory) : PageModel
{
    public ProfessionalDetails Professional { get; private set; } = null!;
    public ClinicalLinks Records { get; private set; } = new([], 1, false);
    public async Task<IActionResult> OnGetAsync(Guid id, int pagina = 1)
    {
        if (!HistoryPaging.IsValid(pagina)) return BadRequest();
        var profile = await professionals.GetAsync(id, HttpContext.RequestAborted); if (profile is null) return NotFound();
        Professional = profile; Records = await directory.RecordsAsync(professionalId: id, page: pagina, ct: HttpContext.RequestAborted); return Page();
    }
}
