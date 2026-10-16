using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Customers;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Customers;

namespace OpticaSosa.Web.Pages.Clinical;

[Authorize(Policy = Permissions.WriteClinical)]
public sealed class RecordEditorModel(IClinicalService clinical, IProfessionalService professionals, ICustomerService customers,
    IClinicalAttachmentService attachments, IClinicalFilePolicyService policy) : PageModel
{
    [BindProperty] public ClinicalInput Input { get; set; } = new();
    [BindProperty] public string? RegistrationType { get; set; }
    [BindProperty] public List<IFormFile> Documents { get; set; } = [];
    public AttachmentPolicyDetails Limits { get; private set; } = null!;
    [BindProperty, System.ComponentModel.DataAnnotations.StringLength(120)] public string? ProfessionalSearch { get; set; }
    public CustomerSummary Customer { get; private set; } = null!;
    public IReadOnlyList<ProfessionalDetails> Professionals { get; private set; } = [];
    public IReadOnlyList<VisitSummary> Visits { get; private set; } = [];
    public bool Correcting { get; private set; }
    public async Task<IActionResult> OnGetAsync(Guid customerId, Guid? id, string? tipo)
    {
        if (id is not null)
        {
            var details = await clinical.GetAsync(customerId, id.Value, ct: HttpContext.RequestAborted);
            if (details is null) return NotFound();
            Input = new ClinicalInput { Version = details.Version, Kind = details.Kind, VisitId = details.VisitId,
                Date = details.Data.Date, ProfessionalId = details.Data.ProfessionalId, Source = details.Data.Source,
                Notes = details.Data.Notes, PupillaryDistance = details.Data.PupillaryDistance,
                IsFollowUp = details.Data.IsFollowUp, ReportedSymptoms = details.Data.ReportedSymptoms, ReportedBy = details.Data.ReportedBy,
                Right = EyeInput.From(details.Data.Right), Left = EyeInput.From(details.Data.Left) };
        }
        else RegistrationType = tipo ?? "medical";
        return await LoadAsync(customerId, id) ? Page() : NotFound();
    }
    public async Task<IActionResult> OnPostAsync(Guid customerId, Guid? id)
    {
        if (!await LoadAsync(customerId, id)) return NotFound();
        if (id is null && RegistrationType is not null)
        {
            Input.Kind = RegistrationType switch { "medical" => OpticaSosa.Domain.Clinical.ClinicalKind.MedicalPrescription,
                "optical" => OpticaSosa.Domain.Clinical.ClinicalKind.OpticalPrescription,
                "history" or "followup" => OpticaSosa.Domain.Clinical.ClinicalKind.VisualHistory, _ => (OpticaSosa.Domain.Clinical.ClinicalKind)(-1) };
            Input.IsFollowUp = RegistrationType == "followup";
        }
        if (!ModelState.IsValid) return Page();
        var result = id is null ? await clinical.CreateAsync(customerId, Input, HttpContext.RequestAborted)
            : await clinical.CorrectAsync(customerId, id.Value, Input, HttpContext.RequestAborted);
        if (result.Code == ClinicalResultCode.NotFound) return NotFound();
        if (!result.Succeeded) { ModelState.AddModelError(string.Empty, result.Error!); return Page(); }
        var saved = 0; var failed = 0; string? firstError = null;
        for (var i = 0; i < Documents.Count; i++)
        {
            var document = Documents[i];
            var details = (await clinical.GetAsync(customerId, result.Id!.Value, ct: HttpContext.RequestAborted))!;
            var digest = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{Input.SubmissionId:N}:{i}"));
            await using var content = document.OpenReadStream();
            var upload = await attachments.UploadAsync(customerId, details.Id,
                new(new Guid(digest.AsSpan(0, 16)), Input.SubmissionId, details.Version, document.FileName, document.ContentType, document.Length, content), HttpContext.RequestAborted);
            if (upload.Succeeded) saved++; else { failed++; firstError ??= upload.Error; }
        }
        TempData["ClinicalSaveMessage"] = failed == 0 ? $"Registro clínico guardado. Documentos adjuntados: {saved}." :
            $"Registro clínico guardado. Adjuntos guardados: {saved}; pendientes: {failed}. {firstError} Conservá los originales y completá la carga desde Adjuntos del registro.";
        return Redirect($"/clientes/{customerId}#" + (Input.Kind == OpticaSosa.Domain.Clinical.ClinicalKind.VisualHistory ? "antecedentes" : "recetas"));
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
        Limits = await policy.GetAsync(HttpContext.RequestAborted);
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
