using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpticaSosa.Application.Security;
using OpticaSosa.Infrastructure.Identity;

namespace OpticaSosa.Web.Pages.Account;

[Authorize]
public sealed class LogoutModel(SignInManager<ApplicationUser> signIn, ISecurityAudit audit) : PageModel
{
    public IActionResult OnGet() => StatusCode(405);
    public async Task<IActionResult> OnPostAsync()
    {
        await audit.RecordAsync("logout", User.FindFirstValue(ClaimTypes.NameIdentifier));
        await signIn.SignOutAsync();
        return LocalRedirect("/cuenta/ingresar");
    }
}
