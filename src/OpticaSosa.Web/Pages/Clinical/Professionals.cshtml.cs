using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Security;

namespace OpticaSosa.Web.Pages.Clinical;

[Authorize(Policy = Permissions.ReadClinical)]
public sealed class ProfessionalsModel(IProfessionalService professionals) : PageModel
{
    [BindProperty] public string? Search { get; set; }
    [TempData] public string? Message { get; set; }
    public IReadOnlyList<ProfessionalDetails> Items { get; private set; } = [];
    public async Task OnGetAsync() => Items = await professionals.SearchAsync(null, HttpContext.RequestAborted);
    public async Task OnPostAsync()
    {
        if ((Search?.Length ?? 0) > 120) ModelState.AddModelError(nameof(Search), "La búsqueda admite hasta 120 caracteres.");
        if (ModelState.IsValid) Items = await professionals.SearchAsync(Search, HttpContext.RequestAborted);
    }
}
