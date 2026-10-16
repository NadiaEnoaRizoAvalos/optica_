using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Security;

namespace OpticaSosa.Web.Pages.Clinical;

[Authorize(Policy = Permissions.WriteClinical)]
public sealed class AttachmentsModel(IClinicalService clinical, IClinicalAttachmentService attachments, IClinicalFilePolicyService policy) : PageModel
{
    [BindProperty] public Guid SubmissionId { get; set; } = Guid.NewGuid();
    [BindProperty] public Guid RecordVersion { get; set; }
    [BindProperty] public Guid RevisionId { get; set; }
    [BindProperty] public IFormFile? Document { get; set; }
    [BindProperty] public Guid AttachmentId { get; set; }
    [BindProperty] public Guid AttachmentVersion { get; set; }
    [BindProperty] public string? RetirementReason { get; set; }
    public ClinicalDetails Details { get; private set; } = null!;
    public AttachmentPage Files { get; private set; } = new([], 1, false);
    public AttachmentPolicyDetails Limits { get; private set; } = null!;
    [TempData] public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid customerId, Guid id, int? revision, int pagina = 1)
    {
        if (!HistoryPaging.IsValid(pagina)) return BadRequest();
        if (!await LoadAsync(customerId, id, revision, pagina)) return NotFound();
        RecordVersion = Details.Version; RevisionId = Details.RevisionId;
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(Guid customerId, Guid id, int? revision, int pagina = 1)
    {
        if (!HistoryPaging.IsValid(pagina)) return BadRequest();
        if (!await LoadAsync(customerId, id, revision, pagina)) return NotFound();
        if (Document is null) ModelState.AddModelError(nameof(Document), "Seleccioná una fotografía o un PDF.");
        if (RevisionId != Details.RevisionId) ModelState.AddModelError(string.Empty, "Recargá la versión del registro antes de adjuntar.");
        if (ModelState.IsValid)
        {
            await using var stream = Document!.OpenReadStream();
            var result = await attachments.UploadAsync(customerId, id,
                new(SubmissionId, RevisionId, RecordVersion, Document.FileName, Document.ContentType, Document.Length, stream), HttpContext.RequestAborted);
            if (result.Succeeded) { Message = "Documento original guardado y verificado."; return RedirectToPage(new { customerId, id, revision = Details.Number }); }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        return Page();
    }
    public async Task<IActionResult> OnPostOptimizeAsync(Guid customerId, Guid id, int? revision, int pagina = 1)
    {
        if (!HistoryPaging.IsValid(pagina)) return BadRequest();
        if (!await LoadAsync(customerId, id, revision, pagina)) return NotFound();
        if (Document is null) { ModelState.AddModelError(nameof(Document), "Seleccioná el PNG original para ofrecer una copia optimizada."); return Page(); }
        await using var stream = Document.OpenReadStream();
        var result = await attachments.OptimizeAsync(customerId, id,
            new(SubmissionId, Details.RevisionId, Details.Version, Document.FileName, Document.ContentType, Document.Length, stream), HttpContext.RequestAborted);
        if (result.Bytes is null) { ModelState.AddModelError(string.Empty, result.Error!); return Page(); }
        Response.Headers.ContentSecurityPolicy = "sandbox; default-src 'none'";
        return File(result.Bytes, "image/png", result.FileName);
    }
    public async Task<IActionResult> OnPostRetireAsync(Guid customerId, Guid id, int? revision, int pagina = 1)
    {
        if (!HistoryPaging.IsValid(pagina)) return BadRequest();
        if (!await LoadAsync(customerId, id, revision, pagina)) return NotFound();
        var result = await attachments.RetireAsync(customerId, id, AttachmentId, AttachmentVersion, RetirementReason ?? "", HttpContext.RequestAborted);
        if (result.Succeeded) { Message = "Documento marcado como retirado; el original y su trazabilidad se conservan."; return RedirectToPage(new { customerId, id, revision = Details.Number }); }
        ModelState.AddModelError(string.Empty, result.Error!); return Page();
    }
    private async Task<bool> LoadAsync(Guid customerId, Guid id, int? revision, int pagina)
    {
        var details = await clinical.GetAsync(customerId, id, revision, ct: HttpContext.RequestAborted);
        if (details is null) return false;
        Details = details;
        Files = await attachments.ListAsync(customerId, id, details.RevisionId, pagina, HttpContext.RequestAborted);
        Limits = await policy.GetAsync(HttpContext.RequestAborted);
        return true;
    }
    public async Task<IActionResult> OnPostRecoverAsync(Guid customerId, Guid id, int? revision, int pagina = 1)
    {
        if (!HistoryPaging.IsValid(pagina)) return BadRequest();
        if (!await LoadAsync(customerId, id, revision, pagina)) return NotFound();
        var result = await attachments.RecoverAsync(customerId, id, AttachmentId, AttachmentVersion, HttpContext.RequestAborted);
        if (result.Succeeded) { Message = "Documento original recuperado y verificado."; return RedirectToPage(new { customerId, id, revision = Details.Number }); }
        ModelState.AddModelError(string.Empty, result.Error!); return Page();
    }
}
