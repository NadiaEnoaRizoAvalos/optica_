using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Security;
using Microsoft.Extensions.Options;

namespace OpticaSosa.Web.Pages.Admin;

[Authorize(Policy = Permissions.ManageBusiness)]
public sealed class ClinicalFilesModel(IClinicalFilePolicyService policy, IClinicalSnapshotService snapshots,
    IStorageCapacity capacity, IOptions<ClinicalFileOptions> options) : PageModel
{
    [BindProperty] public int PhotoMiB { get; set; }
    [BindProperty] public int PdfMiB { get; set; }
    [BindProperty] public Guid Version { get; set; }
    public int TechnicalRequestMiB { get; private set; }
    public long AvailableMiB => capacity.AvailableBytes / ClinicalFileOptions.MiB;
    public int ReserveMiB => options.Value.ReserveDiskMiB;
    [TempData] public string? Message { get; set; }
    public async Task OnGetAsync()
    {
        var settings = await policy.GetAsync(HttpContext.RequestAborted);
        PhotoMiB = settings.PhotoMiB; PdfMiB = settings.PdfMiB; Version = settings.Version;
        TechnicalRequestMiB = settings.TechnicalRequestMiB;
    }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) { TechnicalRequestMiB = (await policy.GetAsync()).TechnicalRequestMiB; return Page(); }
        var result = await policy.SaveAsync(PhotoMiB, PdfMiB, Version, HttpContext.RequestAborted);
        if (result.Succeeded) { Message = "Límites actualizados. Los originales ya guardados no se modifican ni se bloquean por su tamaño anterior."; return RedirectToPage(); }
        ModelState.AddModelError(string.Empty, result.Error!);
        TechnicalRequestMiB = (await policy.GetAsync()).TechnicalRequestMiB;
        return Page();
    }
    public async Task<IActionResult> OnPostBackupAsync()
    {
        try
        {
            var result = await snapshots.CreateAsync(HttpContext.RequestAborted);
            Message = $"Copia local completa y verificada: {result.Directory}. Archivos incluidos: {result.Files}.";
            return RedirectToPage();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ModelState.AddModelError(string.Empty, "La copia no se completó. Revisá espacio y permisos del almacenamiento; una carpeta sin complete.json no es un respaldo válido."); }
        await OnGetAsync(); return Page();
    }
}
