using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpticaSosa.Application.Business;
using OpticaSosa.Application.Security;

namespace OpticaSosa.Web.Pages.Admin;

[Authorize(Policy = Permissions.ManageBusiness)]
[RequestSizeLimit(3 * 1024 * 1024)]
[RequestFormLimits(MultipartBodyLengthLimit = 3 * 1024 * 1024)]
public sealed class BusinessModel(IBusinessSettingsService settings) : PageModel
{
    [BindProperty] public BusinessInput Input { get; set; } = new();
    [BindProperty] public IFormFile? Logo { get; set; }
    [TempData] public string? Message { get; set; }
    public BusinessBranding Branding { get; private set; } = BusinessBranding.Default;

    public async Task OnGetAsync()
    {
        var details = await settings.GetForAdministrationAsync(HttpContext.RequestAborted);
        Input = BusinessInput.From(details.Profile, details.Version);
        Branding = await settings.GetBrandingAsync(HttpContext.RequestAborted);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        LogoUpload? upload = null;
        if (Logo is not null)
        {
            if (Logo.Length is <= 0 or > LogoUpload.MaximumBytes)
                ModelState.AddModelError(string.Empty, "El logo debe ocupar como máximo 2 MB.");
            else
            {
                using var buffer = new MemoryStream();
                await Logo.CopyToAsync(buffer, HttpContext.RequestAborted);
                upload = new(buffer.ToArray(), Logo.FileName, Logo.ContentType);
            }
        }
        if (ModelState.IsValid)
        {
            var result = await settings.SaveAsync(Input.ToProfile(), Input.Version, upload, Input.RemoveLogo, HttpContext.RequestAborted);
            if (result.Succeeded)
            {
                Message = "Configuración guardada. La cabecera ya utiliza los datos del negocio.";
                return RedirectToPage();
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        Branding = await settings.GetBrandingAsync(HttpContext.RequestAborted);
        return Page();
    }
}
