using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using OpticaSosa.Infrastructure.Identity;
using OpticaSosa.Infrastructure.Viewer;

namespace OpticaSosa.Viewer.Pages;

[AllowAnonymous]
[EnableRateLimiting("viewer-login")]
public sealed class LoginModel(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
    ViewerAccess access, ViewerJournal audit, TimeProvider clock) : PageModel
{
    [BindProperty, Required, StringLength(64)] public string UserName { get; set; } = "";
    [BindProperty, Required, StringLength(256)] public string Password { get; set; } = "";
    public void OnGet() { }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var user = await users.FindByNameAsync(UserName.Trim());
        var valid = user is not null && user.IsActive && !access.IsRevoked(user.Id) && !access.IsLocked(user.Id) &&
            (!user.LockoutEnabled || user.LockoutEnd is null || user.LockoutEnd <= clock.GetUtcNow()) &&
            user.PasswordHash is not null && users.PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, Password) != PasswordVerificationResult.Failed;
        if (!valid)
        {
            if (user is not null) access.Failed(user.Id);
            await audit.RecordAsync("viewer.login.failed", null, ct: HttpContext.RequestAborted);
            ModelState.AddModelError("", "No se pudo ingresar. Revisá tus datos, vigencia del acceso o intentá más tarde.");
            return Page();
        }
        await audit.RecordAsync("viewer.login.succeeded", user!.Id, ct: HttpContext.RequestAborted);
        access.Succeeded(user.Id);
        await signIn.SignInAsync(user, isPersistent: false); // No rehash ni contadores en el snapshot.
        return LocalRedirect("/");
    }
}
