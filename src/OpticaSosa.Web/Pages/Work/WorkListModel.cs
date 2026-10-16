using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpticaSosa.Application.Work;
using OpticaSosa.Application.Security;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Domain.Work;
namespace OpticaSosa.Web.Pages.Work;
[Authorize(Policy = Permissions.ReadOrders)]
public sealed class WorkListModel(IWorkService works) : PageModel
{
    public WorkPage Results { get; private set; } = new([], 1, false);
    public string Title => Request.Path.Value switch { "/calidad" => "Control de calidad", "/entregas" => "Entregas", _ => "Pedidos" };
    public WorkStatus? Filter { get; private set; }
    public Guid? CustomerId { get; private set; }
    public async Task<IActionResult> OnGetAsync(Guid? cliente, WorkStatus? estado, int pagina = 1)
    {
        if (!ModelState.IsValid || !HistoryPaging.IsValid(pagina) || estado.HasValue && !Enum.IsDefined(estado.Value)) return BadRequest();
        Filter = estado ?? (Title == "Entregas" ? WorkStatus.ReadyForDelivery : Title == "Control de calidad" ? WorkStatus.PendingControl : null);
        CustomerId = cliente; Results = await works.ListAsync(cliente, Filter, pagina, HttpContext.RequestAborted); return Page();
    }
}
