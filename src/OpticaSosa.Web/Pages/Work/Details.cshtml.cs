using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpticaSosa.Application.Work;
using OpticaSosa.Application.Security;
using OpticaSosa.Application.Clinical;
namespace OpticaSosa.Web.Pages.Work;
[Authorize(Policy = Permissions.ReadOrders)]
public sealed class DetailsModel(IWorkService works, ICurrentUser current) : PageModel
{
    public WorkDetails Details { get; private set; } = null!;
    [BindProperty] public Guid Version { get; set; }
    [BindProperty] public string Command { get; set; } = "";
    public bool Can(string permission) => current.HasPermission(permission);
    public async Task<IActionResult> OnGetAsync(Guid id, int pagina = 1)
    {
        if (!HistoryPaging.IsValid(pagina)) return BadRequest();
        var d = await works.GetAsync(id, pagina, HttpContext.RequestAborted); if (d is null) return NotFound();
        Details = d; Version = d.Work.Version; return Page();
    }
    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        if (!ModelState.IsValid) return BadRequest();
        var r = await works.TransitionAsync(id, Version, Command, HttpContext.RequestAborted);
        if (r.Code == ClinicalResultCode.NotFound) return NotFound();
        if (r.Succeeded) return Redirect($"/pedidos/{id}");
        ModelState.AddModelError("", r.Error!); return await OnGetAsync(id);
    }
}
