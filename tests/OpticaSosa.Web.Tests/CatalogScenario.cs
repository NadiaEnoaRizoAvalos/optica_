using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Application.Security;
using OpticaSosa.Infrastructure.Catalog;
using OpticaSosa.Infrastructure.Identity;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Web.Tests;

internal sealed class CatalogScenario : IDisposable
{
    private readonly IServiceScope scope;
    public IDbContextFactory<OpticaDbContext> Factory { get; }
    public SupplierService Suppliers { get; }
    public CatalogService Catalog { get; }
    public TechnicalCatalog Technical { get; }
    public string ActorId { get; }

    private CatalogScenario(IServiceScope scope, string actorId, IReadOnlyCollection<string> roles)
    {
        this.scope = scope; ActorId = actorId;
        Factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<OpticaDbContext>>();
        var actor = new Actor(actorId, roles);
        Suppliers = new(Factory, actor, TimeProvider.System);
        Catalog = new(Factory, actor, TimeProvider.System);
        Technical = new(Factory, actor);
    }

    public static async Task<CatalogScenario> OpenAsync(SecurityApp app, string userName = "admin.ficticio")
    {
        var scope = app.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await manager.FindByNameAsync(userName))!;
        return new(scope, user.Id, (await manager.GetRolesAsync(user)).ToArray());
    }

    public void Dispose() => scope.Dispose();
    private sealed record Actor(string Id, IReadOnlyCollection<string> Roles) : ICurrentUser
    {
        public bool HasPermission(string permission) => Roles.Any(x => Permissions.Grants(x, permission));
    }
}
