using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Application.Security;
using OpticaSosa.Infrastructure.Identity;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Web.Tests;

internal sealed class AccountScenario : IDisposable
{
    private readonly IServiceScope scope;
    public AccountService Accounts { get; }
    public UserManager<ApplicationUser> Users => scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    public OpticaDbContext Db => scope.ServiceProvider.GetRequiredService<OpticaDbContext>();

    private AccountScenario(IServiceScope scope, ICurrentUser actor)
    {
        this.scope = scope;
        Accounts = new(Db, Users, actor);
    }

    public static async Task<AccountScenario> OpenAsync(SecurityApp app, string name = "admin.ficticio")
    {
        var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByNameAsync(name))!;
        return new(scope, new Actor(user.Id, (await users.GetRolesAsync(user)).ToArray()));
    }

    public async Task<AccountSummary> SummaryAsync(string name) =>
        (await Accounts.ListAsync()).Single(x => x.UserName == name);

    public async Task<AccountResult> SetRolesAsync(string name, params string[] roles)
    {
        var summary = await SummaryAsync(name);
        return await Accounts.ChangeRolesAsync(summary.Id, summary.ConcurrencyStamp, roles, true);
    }

    public void Dispose() => scope.Dispose();
    private sealed record Actor(string Id, string[] Roles) : ICurrentUser
    {
        public bool HasPermission(string permission) => Roles.Any(x => Permissions.Grants(x, permission));
    }
}
