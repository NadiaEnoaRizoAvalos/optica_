using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Security;

namespace OpticaSosa.Web.Pages.Clinical;

[Authorize(Policy = Permissions.WriteClinical)]
public sealed class ProfessionalEditorModel(IProfessionalService professionals) : PageModel
{
    [BindProperty] public ProfessionalInput Input { get; set; } = new();
    public bool IsNew { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid? id)
    {
        IsNew = id is null;
        if (id is null) return Page();
        var details = await professionals.GetAsync(id.Value, HttpContext.RequestAborted);
        if (details is null) return NotFound();
        Input = new ProfessionalInput { FullName = details.FullName, Registration = details.Registration,
            Jurisdiction = details.Jurisdiction, IsActive = details.IsActive, Version = details.Version };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid? id)
    {
        IsNew = id is null;
        if (!ModelState.IsValid) return Page();
        var result = id is null
            ? await professionals.CreateAsync(Input, HttpContext.RequestAborted)
            : await professionals.UpdateAsync(id.Value, Input, HttpContext.RequestAborted);
        if (result.Code == ClinicalResultCode.NotFound) return NotFound();
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return Page();
        }
        TempData["Message"] = "Profesional guardado.";
        return RedirectToPage("Professionals");
    }
}
