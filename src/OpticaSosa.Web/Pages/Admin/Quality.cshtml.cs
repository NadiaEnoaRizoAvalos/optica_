using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpticaSosa.Application.Work;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Work;
namespace OpticaSosa.Web.Pages.Admin;
[Authorize(Policy = Permissions.ConfigureQuality)]
public sealed class QualityModel(IWorkService works) : PageModel
{
    [BindProperty(SupportsGet = true)] public OpticalWorkType Type { get; set; }
    [BindProperty(SupportsGet = true)] public QualityStage Stage { get; set; }
    [BindProperty] public Guid Version { get; set; }
    [BindProperty] public List<string> RequiredChecks { get; set; } = [];
    public TemplateDetails Template { get; private set; } = null!;
    public async Task<IActionResult> OnGetAsync()
    {
        if (!ModelState.IsValid || !Enum.IsDefined(Type) || !Enum.IsDefined(Stage)) return BadRequest();
        Template = await works.TemplateAsync(Type, Stage, HttpContext.RequestAborted); Version = Template.Version;
        RequiredChecks = Template.Items.Where(x => x.Required).Select(x => x.Key).ToList(); return Page();
    }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid || !Enum.IsDefined(Type) || !Enum.IsDefined(Stage)) return BadRequest();
        var r = await works.SaveTemplateAsync(Type, Stage, Version, RequiredChecks, HttpContext.RequestAborted);
        if (r.Succeeded) { TempData["Message"] = "Requisitos guardados. Los pedidos pendientes de ese tipo requieren controles vigentes."; return RedirectToPage(new { Type, Stage }); }
        ModelState.AddModelError("", r.Error!); Template = await works.TemplateAsync(Type, Stage, HttpContext.RequestAborted); return Page();
    }
}
