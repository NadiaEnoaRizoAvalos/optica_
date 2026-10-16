using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpticaSosa.Application.Security;

namespace OpticaSosa.Web.Pages.Admin;

[Authorize(Policy = Permissions.ManageUsers)]
public sealed class UsersModel(IAccountService accounts) : PageModel
{
    [BindProperty, Required, StringLength(64)] public string UserName { get; set; } = string.Empty;
    [BindProperty, Required, StringLength(256, MinimumLength = 12)] public string Password { get; set; } = string.Empty;
    [BindProperty, Required] public string Role { get; set; } = Roles.Sales;
    public IReadOnlyList<AccountSummary> Accounts { get; private set; } = [];
    [TempData] public string? Message { get; set; }

    public async Task OnGetAsync() => Accounts = await accounts.ListAsync(HttpContext.RequestAborted);

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (ModelState.IsValid)
        {
            var result = await accounts.CreateAsync(UserName, Password, Role, HttpContext.RequestAborted);
            if (result.Succeeded) { Message = "Usuario creado."; return RedirectToPage(); }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        Accounts = await accounts.ListAsync(HttpContext.RequestAborted);
        return Page();
    }

    public async Task<IActionResult> OnPostDeactivateAsync(string userId)
    {
        var result = await accounts.DeactivateAsync(userId, HttpContext.RequestAborted);
        Message = result.Succeeded ? "Usuario inactivado. Su sesión ya no permite acceder." : result.Error;
        return RedirectToPage();
    }
}
