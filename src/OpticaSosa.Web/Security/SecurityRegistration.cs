using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using OpticaSosa.Application.Security;
using OpticaSosa.Infrastructure.Identity;

namespace OpticaSosa.Web.Security;

public static class SecurityRegistration
{
    public static IServiceCollection AddLocalSecurity(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddIdentityCore<ApplicationUser>()
            .AddSignInManager<ActiveSignInManager>()
            .AddDefaultTokenProviders();
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
            options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
        }).AddIdentityCookies();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "OpticaSosa.Session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.LoginPath = "/cuenta/ingresar";
            options.AccessDeniedPath = "/cuenta/denegado";
            options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            options.SlidingExpiration = true;
            options.Events.OnRedirectToAccessDenied = async context =>
            {
                var audit = context.HttpContext.RequestServices.GetRequiredService<ISecurityAudit>();
                await audit.RecordAsync("authorization.denied", context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
                context.Response.Redirect(context.RedirectUri);
            };
            options.Events.OnValidatePrincipal = async context =>
            {
                await SecurityStampValidator.ValidatePrincipalAsync(context);
                if (context.Principal?.Identity?.IsAuthenticated != true) return;
                var manager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await manager.GetUserAsync(context.Principal);
                if (user is null || !user.IsActive)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                }
            };
        });
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            foreach (var permission in new[] { Permissions.ManageBackups, Permissions.VerifyBackups, Permissions.ManageUsers, Permissions.ReadAudit, Permissions.ReadClinical,
                         Permissions.WriteClinical, Permissions.ReadCustomers, Permissions.WriteCustomers,
                         Permissions.DeactivateCustomers, Permissions.ManageBusiness, Permissions.ReadOrders, Permissions.WriteOrders,
                         Permissions.TechnicalControl, Permissions.ApproveTechnical, Permissions.DeliveryControl,
                         Permissions.ApproveDelivery, Permissions.DeliverOrders, Permissions.ConfigureQuality })
                options.AddPolicy(permission, policy => policy.RequireAuthenticatedUser().RequireRole(
                    Roles.All.Where(role => Permissions.Grants(role, permission)).ToArray()));
        });
        services.AddCascadingAuthenticationState();
        return services;
    }
}
