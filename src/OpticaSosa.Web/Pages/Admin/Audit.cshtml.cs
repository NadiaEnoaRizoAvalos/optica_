using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpticaSosa.Application.Security;

namespace OpticaSosa.Web.Pages.Admin;

[Authorize(Policy = Permissions.ReadAudit)]
public sealed class AuditModel(IAccountService accounts) : PageModel
{
    public IReadOnlyList<AuditSummary> Entries { get; private set; } = [];
    public async Task OnGetAsync() => Entries = await accounts.ReadAuditAsync(HttpContext.RequestAborted);
}
