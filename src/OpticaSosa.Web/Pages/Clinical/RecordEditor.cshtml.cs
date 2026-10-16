using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Customers;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Customers;

namespace OpticaSosa.Web.Pages.Clinical;

[Authorize(Policy = Permissions.WriteClinical)]
public sealed class RecordEditorModel(IClinicalService clinical, IProfessionalService professionals, ICustomerService customers) : PageModel
{
    [BindProperty] public ClinicalInput Input { get; set; } = new();
    [BindProperty, System.ComponentModel.DataAnnotations.StringLength(120)] public string? ProfessionalSearch { get; set; }
    public CustomerSummary Customer { get; private set; } = null!;
    public IReadOnlyList<ProfessionalDetails> Professionals { get; private set; } = [];
    public IReadOnlyList<VisitSummary> Visits { get; private set; } = [];
    public bool Correcting { get; private set; }
    public async Task<IActionResult> OnGetAsync(Guid customerId, Guid? id)
    {
        if (id is not null)
        {
            var details = await clinical.GetAsync(customerId, id.Value, ct: HttpContext.RequestAborted);
            if (details is null) return NotFound();
            Input = new ClinicalInput { Version = details.Version, Kind = details.Kind, VisitId = details.VisitId,
                Date = details.Data.Date, ProfessionalId = details.Data.ProfessionalId, Source = details.Data.Source,
                Notes = details.Data.Notes, PupillaryDistance = details.Data.PupillaryDistance,
                Right = EyeInput.From(details.Data.Right), Left = EyeInput.From(details.Data.Left) };
        }
        return await LoadAsync(customerId, id) ? Page() : NotFound();
    }
    public async Task<IActionResult> OnPostAsync(Guid customerId, Guid? id)
    {
        if (!await LoadAsync(customerId, id)) return NotFound();
        if (!ModelState.IsValid) return Page();
        var result = id is null ? await clinical.CreateAsync(customerId, Input, HttpContext.RequestAborted)
            : await clinical.CorrectAsync(customerId, id.Value, Input, HttpContext.RequestAborted);
        if (result.Code == ClinicalResultCode.NotFound) return NotFound();
        if (!result.Succeeded) { ModelState.AddModelError(string.Empty, result.Error!); return Page(); }
        return Redirect($"/clientes/{customerId}/clinica/{result.Id}");
    }
    public async Task<IActionResult> OnPostSearchProfessionalsAsync(Guid customerId, Guid? id)
    {
        if ((ProfessionalSearch?.Length ?? 0) > 120)
        {
            ProfessionalSearch = null;
            ModelState.AddModelError(nameof(ProfessionalSearch), "La búsqueda admite hasta 120 caracteres.");
        }
        return await LoadAsync(customerId, id) ? Page() : NotFound();
    }
    private async Task<bool> LoadAsync(Guid customerId, Guid? id)
    {
        Correcting = id is not null;
        var customer = await customers.GetAsync(customerId, ct: HttpContext.RequestAborted);
        if (customer is null) return false;
        Customer = customer.Customer;
        Visits = customer.Visits.Where(x => x.Status != VisitStatus.Cancelled).ToArray();
        Professionals = (ProfessionalSearch?.Length ?? 0) <= 120
            ? (await professionals.SearchAsync(ProfessionalSearch, HttpContext.RequestAborted)).Where(x => x.IsActive).ToArray()
            : [];
        if (Input.ProfessionalId != Guid.Empty && Professionals.All(x => x.Id != Input.ProfessionalId))
        {
            var selected = await professionals.GetAsync(Input.ProfessionalId, HttpContext.RequestAborted);
            if (selected?.IsActive == true) Professionals = Professionals.Append(selected).ToArray();
        }
        return true;
    }
}
