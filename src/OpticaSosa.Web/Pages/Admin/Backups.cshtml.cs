using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpticaSosa.Application.Backups;
using OpticaSosa.Application.Security;

namespace OpticaSosa.Web.Pages.Admin;

[Authorize(Policy = Permissions.ManageBackups)]
public sealed class BackupsModel(IBackupService backups) : PageModel
{
    public BackupOverview Overview { get; private set; } = null!;
    public IReadOnlyList<BackupTarget> Targets { get; private set; } = [];
    [BindProperty] public bool Enabled { get; set; }
    [BindProperty] public int EveryDays { get; set; } = 1;
    [BindProperty] public string? LocalTime { get; set; }
    [BindProperty] public string TimeZoneId { get; set; } = TimeZoneInfo.Local.Id;
    [BindProperty] public string? VolumeId { get; set; }
    [BindProperty] public int KeepCopies { get; set; } = 30;
    [BindProperty] public Guid Version { get; set; }
    [TempData] public string? Message { get; set; }

    public async Task OnGetAsync()
    {
        await LoadAsync(); var s = Overview.Configuration;
        Enabled = s.Enabled; EveryDays = s.EveryDays; LocalTime = s.LocalTime; TimeZoneId = s.TimeZoneId;
        VolumeId = s.VolumeId; KeepCopies = s.KeepCopies; Version = s.Version;
    }
    public async Task<IActionResult> OnPostInspectAsync()
    {
        await OnGetAsync();
        try { Targets = await backups.InspectTargetsAsync(HttpContext.RequestAborted); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { Message = "No se pudo verificar la identidad física de los discos; no se modificó ninguno."; }
        return Page();
    }
    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (ModelState.IsValid)
        {
            var result = await backups.ConfigureAsync(new(Enabled, EveryDays, LocalTime, TimeZoneId,
                string.IsNullOrWhiteSpace(VolumeId) ? null : VolumeId, KeepCopies, Version), HttpContext.RequestAborted);
            Message = result.Message;
            if (result.Succeeded) return RedirectToPage();
        }
        await LoadAsync(); return Page();
    }
    public async Task<IActionResult> OnPostManualAsync()
    { Message = (await backups.RequestAsync(HttpContext.RequestAborted)).Message; return RedirectToPage(); }
    public async Task<IActionResult> OnPostVerifyAsync(Guid runId)
    { Message = (await backups.VerifyAsync(runId, HttpContext.RequestAborted)).Message; return RedirectToPage(); }
    private async Task LoadAsync() => Overview = await backups.OverviewAsync(HttpContext.RequestAborted);
}
