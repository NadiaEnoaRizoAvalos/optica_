using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpticaSosa.Application.Security;
using OpticaSosa.Infrastructure.Identity;

namespace OpticaSosa.Web.Pages.Account;

[AllowAnonymous]
public sealed class LoginModel(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, ISecurityAudit audit) : PageModel
{
    [BindProperty, Required, StringLength(64)] public string UserName { get; set; } = string.Empty;
    [BindProperty, Required, StringLength(256)] public string Password { get; set; } = string.Empty;

    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true ? LocalRedirect("/") : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var user = await users.FindByNameAsync(UserName.Trim());
        var result = user is null ? Microsoft.AspNetCore.Identity.SignInResult.Failed
            : await signIn.CheckPasswordSignInAsync(user, Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            await audit.RecordAsync("login.failed", null);
            ModelState.AddModelError(string.Empty, "No se pudo ingresar. Revisá tus datos o intentá más tarde.");
            return Page();
        }
        await audit.RecordAsync("login.succeeded", user!.Id);
        await signIn.SignInAsync(user, isPersistent: false);
        return LocalRedirect("/");
    }
}
