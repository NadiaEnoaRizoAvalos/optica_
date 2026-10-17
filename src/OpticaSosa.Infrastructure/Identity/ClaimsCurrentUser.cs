using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using OpticaSosa.Application.Security;

namespace OpticaSosa.Infrastructure.Identity;

public class ClaimsCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;
    public string? Id => Principal?.Identity?.IsAuthenticated == true
        ? Principal.FindFirstValue(ClaimTypes.NameIdentifier) : null;
    public bool HasPermission(string permission) => Id is not null &&
        Roles.All.Any(role => Principal!.IsInRole(role) && Permissions.Grants(role, permission));
}
