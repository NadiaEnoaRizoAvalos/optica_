using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Application.Commerce;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Commerce;
using OpticaSosa.Infrastructure.Commerce;
using OpticaSosa.Infrastructure.Identity;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Web.Tests;

internal sealed class CommerceScenario : IDisposable
{
    private readonly IServiceScope scope;
    public SalesOrderService Orders { get; }
    public OrderPaymentService Payments { get; }
    public IDbContextFactory<OpticaDbContext> Factory { get; }
    public ICurrentUser Actor { get; }
    private CommerceScenario(IServiceScope scope, string actorId, IReadOnlyList<string> roles)
    {
        this.scope = scope; Actor = new CurrentActor(actorId, roles);
        Factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<OpticaDbContext>>();
        Orders = new(Factory, Actor, TimeProvider.System); Payments = new(Factory, Actor, TimeProvider.System);
    }
    public static async Task<CommerceScenario> OpenAsync(SecurityApp app, string name = "admin.ficticio")
    {
        var scope = app.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await manager.FindByNameAsync(name))!;
        return new(scope, user.Id, (await manager.GetRolesAsync(user)).ToArray());
    }
    public async Task<SalesDraftInput> DraftAsync(CatalogScenario catalog, Guid customerId, Guid variantId, decimal price = 100m, int quantity = 1)
    {
        await using var db = await Factory.CreateDbContextAsync();
        var variant = await db.ProductVariants.AsNoTracking().SingleAsync(x => x.Id == variantId);
        return new() { CustomerId = customerId, Currency = "ARS", Lines = [new(variant.Id, variant.Version, quantity, FulfilmentSource.Stock, price)] };
    }
    public async Task<Guid> ConfirmAsync(SalesDraftInput draft)
    {
        var created = await Orders.CreateDraftAsync(draft);
        Xunit.Assert.True(created.Succeeded, created.Error);
        var order = (await Orders.GetAsync(created.OrderId!.Value))!;
        var confirmed = await Orders.ConfirmAsync(order.Id, new(Guid.NewGuid(), order.Version));
        Xunit.Assert.True(confirmed.Succeeded, confirmed.Error); return order.Id;
    }
    public void Dispose() => scope.Dispose();
    private sealed record CurrentActor(string Id, IReadOnlyList<string> Roles) : ICurrentUser
    {
        public bool HasPermission(string permission) => Roles.Any(x => Permissions.Grants(x, permission));
    }
}
