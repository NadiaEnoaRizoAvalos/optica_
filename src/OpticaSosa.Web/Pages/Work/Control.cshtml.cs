using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpticaSosa.Application.Work;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Work;
namespace OpticaSosa.Web.Pages.Work;
[Authorize(Policy = Permissions.ReadOrders)]
public sealed class ControlModel(IWorkService works, ICurrentUser current) : PageModel
{
    [BindProperty] public ControlInput Input { get; set; } = new();
    [BindProperty] public string Command { get; set; } = "save";
    public WorkDetails Details { get; private set; } = null!;
    public TemplateDetails Template { get; private set; } = null!;
    public bool Rectifying => Input.Stage == QualityStage.Technical ? Details.Work.TechnicalApproved : Details.Work.DeliveryApproved;
    public bool CanApprove => current.HasPermission(Input.Stage == QualityStage.Technical ? Permissions.ApproveTechnical : Permissions.ApproveDelivery);
    public bool Complete => Template.Items.All(r => Input.Items.Any(i => i.Key == r.Key && i.Status != CheckStatus.Observed &&
        (r.Applicable ? i.Status != CheckStatus.NotApplicable && (!r.Required || i.Status == CheckStatus.Conforming) :
        i.Status == CheckStatus.NotApplicable && !string.IsNullOrWhiteSpace(i.Notes)))) && (!Rectifying || !string.IsNullOrWhiteSpace(Input.Notes));
    public async Task<IActionResult> OnGetAsync(Guid id, QualityStage etapa)
    {
        Input.Stage = etapa;
        var response = await LoadAsync(id, etapa); if (response is not null) return response;
        Input.WorkVersion = Details.Work.Version; Input.TemplateVersion = Template.Version;
        var last = Details.Controls.FirstOrDefault(x => x.Stage == etapa && x.Round == Details.Work.Round);
        Input.Items = Template.Items.Select(r => { var previous = last?.Items.SingleOrDefault(x => x.Key == r.Key);
            return new CheckInput { Key = r.Key, Status = previous?.Status ?? CheckStatus.Pending, Notes = previous?.Notes }; }).ToList();
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(Guid id, QualityStage etapa)
    {
        var response = await LoadAsync(id, etapa); if (response is not null) return response;
        if (Input.Stage != etapa || Command is not ("save" or "observe" or "approve")) return BadRequest();
        Input.Approve = Command == "approve";
        if (Command == "observe" && !Input.Items.Any(x => x.Status == CheckStatus.Observed)) ModelState.AddModelError("", "Marcá al menos una verificación como Observado.");
        if (!ModelState.IsValid) return Page();
        var result = await works.SaveControlAsync(id, Input, HttpContext.RequestAborted);
        if (result.Code == ClinicalResultCode.NotFound) return NotFound();
        if (!result.Succeeded) { ModelState.AddModelError("", result.Error!); return Page(); }
        return Redirect($"/pedidos/{id}#historial");
    }
    private async Task<IActionResult?> LoadAsync(Guid id, QualityStage stage)
    {
        if (!Enum.IsDefined(stage)) return BadRequest();
        if (!current.HasPermission(stage == QualityStage.Technical ? Permissions.TechnicalControl : Permissions.DeliveryControl)) return Forbid();
        var d = await works.GetAsync(id, ct: HttpContext.RequestAborted); if (d is null) return NotFound();
        Details = d; Template = await works.TemplateAsync(d.Work.Type, stage, HttpContext.RequestAborted);
        if (d.Work.Status is not (WorkStatus.PendingControl or WorkStatus.ReadyForDelivery)) return Redirect($"/pedidos/{id}");
        return null;
    }
}
