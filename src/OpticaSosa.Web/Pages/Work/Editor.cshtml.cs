using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpticaSosa.Application.Work;
using OpticaSosa.Application.Customers;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Security;
namespace OpticaSosa.Web.Pages.Work;
[Authorize(Policy = Permissions.WriteOrders)]
public sealed class EditorModel(IWorkService works, ICustomerService customers, IClinicalDirectory directory, ICurrentUser current) : PageModel
{
    [BindProperty] public WorkInput Input { get; set; } = new();
    [BindProperty, System.ComponentModel.DataAnnotations.StringLength(100)] public string? Search { get; set; }
    public CustomerSearchResult Matches { get; private set; } = new([], false);
    public CustomerSummary? Customer { get; private set; }
    public ClinicalLinks Recipes { get; private set; } = new([], 1, false);
    public bool Clinical => current.HasPermission(Permissions.ReadClinical);
    public bool Editing { get; private set; }
    public async Task<IActionResult> OnGetAsync(Guid? id, Guid? cliente, Guid? receta, int pagina = 1)
    {
        if (!HistoryPaging.IsValid(pagina)) return BadRequest();
        Editing = id.HasValue;
        if (id.HasValue)
        {
            var d = await works.GetAsync(id.Value, ct: HttpContext.RequestAborted); if (d is null) return NotFound();
            Input = new() { Version = d.Work.Version, CustomerId = d.Work.CustomerId, Product = d.Work.Product, Frame = d.Work.Frame, Type = d.Work.Type, PrescriptionId = d.PrescriptionId };
        }
        else { Input.CustomerId = cliente ?? Guid.Empty; Input.PrescriptionId = Clinical ? receta : null; }
        await LoadAsync(pagina); return Page();
    }
    public async Task<IActionResult> OnPostAsync(Guid? id)
    {
        Editing = id.HasValue; await LoadAsync();
        if (!ModelState.IsValid) return Page();
        var r = id.HasValue ? await works.EditAsync(id.Value, Input, HttpContext.RequestAborted) : await works.CreateAsync(Input, HttpContext.RequestAborted);
        if (r.Code == ClinicalResultCode.NotFound) return NotFound();
        if (!r.Succeeded) { ModelState.AddModelError("", r.Error!); return Page(); }
        return Redirect($"/pedidos/{r.Id}");
    }
    public async Task<IActionResult> OnPostSearchAsync()
    {
        if (Search?.Length > 100) ModelState.AddModelError("", "La búsqueda admite hasta 100 caracteres.");
        else Matches = await customers.SearchAsync(Search, ct: HttpContext.RequestAborted);
        return Page();
    }
    private async Task LoadAsync(int page = 1)
    {
        Customer = (await customers.GetAsync(Input.CustomerId, ct: HttpContext.RequestAborted))?.Customer;
        if (Clinical && Customer is not null) Recipes = await directory.RecordsAsync(customerId: Customer.Id, histories: false, page: page, ct: HttpContext.RequestAborted);
    }
}
