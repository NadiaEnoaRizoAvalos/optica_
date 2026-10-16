using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpticaSosa.Application.Security;
using OpticaSosa.Infrastructure.Identity;

namespace OpticaSosa.Web.Pages.Account;

[Authorize]
public sealed class PasswordModel(IAccountService accounts, SignInManager<ApplicationUser> signIn) : PageModel
{
    [BindProperty, Required, StringLength(256)] public string CurrentPassword { get; set; } = string.Empty;
    [BindProperty, Required, StringLength(256, MinimumLength = 12)] public string NewPassword { get; set; } = string.Empty;
    [BindProperty, Required, Compare(nameof(NewPassword), ErrorMessage = "Las contraseñas no coinciden.")]
    public string Confirmation { get; set; } = string.Empty;

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var result = await accounts.ChangePasswordAsync(CurrentPassword, NewPassword, HttpContext.RequestAborted);
        if (!result.Succeeded) { ModelState.AddModelError(string.Empty, result.Error!); return Page(); }
        await signIn.SignOutAsync();
        return LocalRedirect("/cuenta/ingresar");
    }
}
