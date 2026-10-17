using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpticaSosa.Application.Security;
using OpticaSosa.Application.Viewer;

namespace OpticaSosa.Viewer.Pages;

[Authorize(Policy = Permissions.ManageUsers)]
public sealed class AccessModel(IViewerAccounts accounts) : PageModel
{
    public IReadOnlyList<ViewerAccount> Accounts { get; private set; } = [];
    [TempData] public string? Message { get; set; }
    public async Task OnGetAsync() => Accounts = await accounts.ListAsync(HttpContext.RequestAborted);
    public async Task<IActionResult> OnPostAsync(string? userId, bool confirmed)
    {
        try
        {
            var result = await accounts.RevokeAsync(userId, confirmed, HttpContext.RequestAborted);
            Message = result.Succeeded ? "Acceso revocado en este visor. Las sesiones dejan de acceder en la siguiente solicitud. La cuenta original no cambió." : result.Error;
            return RedirectToPage();
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
