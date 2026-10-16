using Microsoft.AspNetCore.Mvc;
using OpticaSosa.Application.Backups;

namespace OpticaSosa.Web.Pages.Admin;

public sealed partial class BackupsModel
{
    public BackupFolderPage? FolderPage { get; private set; }
    public BackupFolderSelection? Selection { get; private set; }
    [BindProperty] public string? SelectedVolumeId { get; set; }
    [BindProperty] public string? SelectedRelativeFolder { get; set; }
    [BindProperty] public string? NewFolderName { get; set; }
    [BindProperty] public bool ConfirmCreate { get; set; }

    public async Task<IActionResult> OnPostBrowseAsync(string? folder, int folderPage = 1)
    {
        await RefreshFoldersAsync(); ModelState.Clear();
        try { FolderPage = await folders.BrowseAsync(VolumeId ?? "", folder ?? "", folderPage, HttpContext.RequestAborted); }
        catch (Exception ex) when (FolderError(ex)) { Message = "No se pudo explorar esa carpeta. Revisá conexión, permisos y disco seleccionado; no se modificó ningún archivo."; }
        return Page();
    }

    public async Task<IActionResult> OnPostSelectFolderAsync(string? folder)
    {
        await RefreshFoldersAsync();
        try
        {
            Selection = await folders.SelectAsync(VolumeId ?? "", folder ?? "", HttpContext.RequestAborted);
            RelativeFolder = Selection.RelativeFolder; SelectedVolumeId = Selection.VolumeId; SelectedRelativeFolder = Selection.RelativeFolder;
            Message = "Carpeta confirmada. Revisá la ruta completa y guardá la configuración; no se activaron respaldos.";
        }
        catch (Exception ex) when (FolderError(ex)) { Message = "No se pudo seleccionar la carpeta. Debe existir dentro del disco permitido, sin enlaces ni directorios protegidos."; }
        ModelState.Clear(); return Page();
    }

    public async Task<IActionResult> OnPostCreateFolderAsync(string? folder)
    {
        var result = await folders.CreateAsync(VolumeId ?? "", folder ?? "", NewFolderName ?? "", ConfirmCreate, HttpContext.RequestAborted);
        var page = await OnPostBrowseAsync(folder);
        Message = result.Message;
        return page;
    }

    private async Task RefreshFoldersAsync()
    {
        await LoadAsync();
        try { Targets = await backups.InspectTargetsAsync(HttpContext.RequestAborted); }
        catch (Exception ex) when (FolderError(ex)) { Message ??= "No se pudieron inspeccionar los discos. No se cambió el destino."; }
        if (VolumeId == SelectedVolumeId && RelativeFolder == SelectedRelativeFolder && VolumeId is not null)
        {
            try { Selection = await folders.SelectAsync(VolumeId, RelativeFolder, HttpContext.RequestAborted); }
            catch (Exception ex) when (FolderError(ex)) { Message ??= "No se pudo confirmar la ruta guardada. Podés desactivar la programación conservando el destino desconectado."; }
        }
    }

    private static bool FolderError(Exception ex) => ex is IOException or UnauthorizedAccessException or ArgumentException;

    private void SpanishValidation()
    {
        var labels = new Dictionary<string, string>
        {
            [nameof(TimeZoneId)] = "Elegí una zona horaria válida.",
            [nameof(RelativeFolder)] = "Explorá y confirmá una carpeta de destino válida.",
            [nameof(EveryDays)] = "Indicá una frecuencia válida, entre 1 y 365 días.",
            [nameof(KeepCopies)] = "Indicá una conservación válida, entre 1 y 3650 copias.",
            [nameof(Version)] = "La configuración cambió. Recargá la pantalla antes de guardar.",
            [nameof(LocalTime)] = "Elegí un horario válido.", [nameof(Enabled)] = "Revisá la activación de la programación."
        };
        foreach (var (key, entry) in ModelState)
        {
            if (entry.Errors.Count == 0) continue;
            entry.Errors.Clear(); entry.Errors.Add(labels.GetValueOrDefault(key, "Revisá los datos de configuración ingresados."));
        }
    }
}
