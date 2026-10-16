using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Application.Customers;
using OpticaSosa.Application.Security;
using OpticaSosa.Infrastructure.Customers;
using OpticaSosa.Infrastructure.Identity;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Web.Tests;

internal sealed class CustomerScenario : IDisposable
{
    private readonly IServiceScope scope;
    public CustomerService Customers { get; }
    public VisitService Visits { get; }
    public OpticaDbContext Db => scope.ServiceProvider.GetRequiredService<OpticaDbContext>();
    public string ActorId { get; }
    public ManualClock Clock { get; } = new();

    private CustomerScenario(IServiceScope scope, string id, IReadOnlyCollection<string> roles)
    {
        this.scope = scope;
        ActorId = id;
        var current = new Actor(id, roles);
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<OpticaDbContext>>();
        Customers = new CustomerService(factory, current, Clock);
        Visits = new VisitService(factory, current, Clock);
    }

    public static async Task<CustomerScenario> OpenAsync(SecurityApp app, string name = "admin.ficticio")
    {
        var scope = app.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await manager.FindByNameAsync(name))!;
        return new(scope, user.Id, (await manager.GetRolesAsync(user)).ToArray());
    }

    public static CustomerInput Input(string document = "FIC-001") => new()
    { FirstName = "Nombre ficticio " + document, LastName = "Prueba", Document = document };
    public void Dispose() => scope.Dispose();
    private sealed record Actor(string Id, IReadOnlyCollection<string> Roles) : ICurrentUser
    {
        public bool HasPermission(string permission) => Roles.Any(role => Permissions.Grants(role, permission));
    }
    internal sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance() => now = now.AddMinutes(1);
    }
}
