using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpticaSosa.Application.Security;
namespace OpticaSosa.Web.Pages.Admin;
[Authorize(Policy = Permissions.ManageBusiness)]
public sealed class OverviewModel : PageModel { public void OnGet() { } }
