using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using OpticaSosa.Application.Backups;
using OpticaSosa.Application.Security;

namespace OpticaSosa.Web.Pages.Admin;

[Authorize(Policy = Permissions.ManageBackups)]
public sealed partial class BackupsModel : PageModel
{
    private readonly IBackupService backups;
    private readonly IBackupFolderService folders;
    public BackupsModel(IBackupService backups, IBackupFolderService folders) { this.backups = backups; this.folders = folders; }
    public BackupOverview Overview { get; private set; } = null!;
    public IReadOnlyList<BackupTarget> Targets { get; private set; } = [];
    [BindProperty, Required(ErrorMessage = "Revisá la activación de la programación.")] public bool Enabled { get; set; }
    [BindProperty, Required(ErrorMessage = "Indicá la frecuencia en días.")] public int EveryDays { get; set; } = 1;
    [BindProperty] public string? LocalTime { get; set; }
    [BindProperty, Required(ErrorMessage = "Elegí una zona horaria válida.")] public string TimeZoneId { get; set; } = TimeZoneInfo.Local.Id;
    [BindProperty] public string? VolumeId { get; set; }
    [BindProperty, Required(ErrorMessage = "Indicá cuántas copias conservar.")] public int KeepCopies { get; set; } = 30;
    [BindProperty, Required(ErrorMessage = "Recargá la configuración antes de guardar.")] public Guid Version { get; set; }
    [BindProperty, Required(ErrorMessage = "Seleccioná una carpeta de destino.")] public string RelativeFolder { get; set; } = OpticaSosa.Domain.Backups.BackupFolder.Default;
    [TempData] public string? Message { get; set; }

    public async Task OnGetAsync()
    {
        await LoadAsync(); var s = Overview.Configuration;
        Enabled = s.Enabled; EveryDays = s.EveryDays; LocalTime = s.LocalTime; TimeZoneId = s.TimeZoneId;
        VolumeId = s.VolumeId; KeepCopies = s.KeepCopies; Version = s.Version;
        RelativeFolder = s.RelativeFolder;
        SelectedVolumeId = s.VolumeId; SelectedRelativeFolder = s.RelativeFolder;
        if (VolumeId is not null) await RefreshFoldersAsync();
    }
    public async Task<IActionResult> OnPostInspectAsync()
    {
        await RefreshFoldersAsync(); ModelState.Clear();
        return Page();
    }
    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (VolumeId is not null && (VolumeId != SelectedVolumeId || RelativeFolder != SelectedRelativeFolder))
            ModelState.AddModelError(nameof(RelativeFolder), "Explorá y confirmá la carpeta del disco seleccionado antes de guardar.");
        SpanishValidation();
        if (ModelState.IsValid)
        {
            var result = await backups.ConfigureAsync(new(Enabled, EveryDays, LocalTime, TimeZoneId,
                string.IsNullOrWhiteSpace(VolumeId) ? null : VolumeId, KeepCopies, Version, RelativeFolder), HttpContext.RequestAborted);
            Message = result.Message;
            if (result.Succeeded) return RedirectToPage();
        }
        await RefreshFoldersAsync(); return Page();
    }
    public async Task<IActionResult> OnPostManualAsync()
    { Message = (await backups.RequestAsync(HttpContext.RequestAborted)).Message; return RedirectToPage(); }
    public async Task<IActionResult> OnPostBindAsync()
    { Message = (await backups.BindDestinationAsync(Version, HttpContext.RequestAborted)).Message; return RedirectToPage(); }
    public async Task<IActionResult> OnPostVerifyAsync(Guid runId)
    { Message = (await backups.VerifyAsync(runId, HttpContext.RequestAborted)).Message; return RedirectToPage(); }
    private async Task LoadAsync() => Overview = await backups.OverviewAsync(HttpContext.RequestAborted);

    public static string DisplayStatus(string status) => status switch
    {
        "Complete" => "Exitoso y verificado", "Failed" => "Fallido", "Queued" => "En espera",
        "Running" => "En curso", "Interrupted" => "Interrumpido", "Corrupt" => "Integridad inválida",
        "Pruned" => "Retirado por conservación", _ => "Estado desconocido"
    };

    public string DisplayDate(DateTime? utc)
    {
        if (utc is null) return "Sin registro";
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(Overview.Configuration.TimeZoneId);
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc), zone);
            return local.ToString("dd/MM/yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + " (" + zone.Id + ")";
        }
        catch (TimeZoneNotFoundException) { return utc.Value.ToString("u"); }
    }
}
