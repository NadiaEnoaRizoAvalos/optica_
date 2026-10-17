using OpticaSosa.Application.Security;
using OpticaSosa.Infrastructure.Identity;

namespace OpticaSosa.Web.Security;

public static class SecurityRegistration
{
    public static IServiceCollection AddLocalSecurity(this IServiceCollection services)
    {
        services.AddLocalIdentitySecurity();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddCascadingAuthenticationState();
        return services;
    }
}
