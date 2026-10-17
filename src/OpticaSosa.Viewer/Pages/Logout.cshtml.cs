using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpticaSosa.Infrastructure.Viewer;

namespace OpticaSosa.Viewer.Pages;

public sealed class LogoutModel(ViewerJournal audit) : PageModel
{
    public IActionResult OnGet() => StatusCode(405);
    public async Task<IActionResult> OnPostAsync()
    {
        await audit.RecordAsync("viewer.logout", User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return LocalRedirect("/cuenta/ingresar");
    }
}
