using Microsoft.AspNetCore.Identity;

namespace OpticaSosa.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public bool IsActive { get; set; } = true;
}
