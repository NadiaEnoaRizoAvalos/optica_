using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Application.Security;
using OpticaSosa.Infrastructure.Identity;
using OpticaSosa.Infrastructure.Persistence;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class MultiRoleTests
{
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public async Task AllSevenCombinationsCreateOneAccountWithExactlyTheSelectedRoles(int mask)
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var scenario = await AccountScenario.OpenAsync(app);
        var roles = Roles.All.Where((_, i) => (mask & (1 << i)) != 0).ToArray();
        Assert.True((await scenario.Accounts.CreateAsync("combinada.ficticia", SecurityApp.TestPassword, roles)).Succeeded);
        var account = await scenario.SummaryAsync("combinada.ficticia");
        Assert.Equal(roles.Order(), account.Roles);
        var user = (await scenario.Users.FindByIdAsync(account.Id))!;
        Assert.True(await scenario.Users.CheckPasswordAsync(user, SecurityApp.TestPassword));
        Assert.Equal(4, (await scenario.Accounts.ListAsync()).Count);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task EmptyUnknownAndDuplicateSelectionsAreRejectedWithoutWrites(int invalid)
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var scenario = await AccountScenario.OpenAsync(app);
        string[] roles = invalid switch { 0 => [], 1 => ["Inventado"], _ => [Roles.Professional, Roles.Professional] };
        var previous = await scenario.SummaryAsync("ventas.ficticio");
        Assert.False((await scenario.Accounts.CreateAsync("invalida.ficticia", SecurityApp.TestPassword, roles)).Succeeded);
        Assert.False((await scenario.Accounts.ChangeRolesAsync(previous.Id, previous.ConcurrencyStamp, roles, true)).Succeeded);
        Assert.Null(await scenario.Users.FindByNameAsync("invalida.ficticia"));
        var after = await scenario.SummaryAsync("ventas.ficticio");
        Assert.Equal(previous.Roles, after.Roles); Assert.Equal(previous.ConcurrencyStamp, after.ConcurrencyStamp);
        Assert.False(await scenario.Db.AuditEntries.AnyAsync(x => x.Action == "user.roles.changed"));
    }

    [Fact]
    public async Task ChangesPreserveIdentifiersCredentialsAndUnrelatedAccountsAndAuditEveryRole()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var scenario = await AccountScenario.OpenAsync(app);
        var user = (await scenario.Users.FindByNameAsync("ventas.ficticio"))!;
        var original = (user.Id, user.PasswordHash, user.UserName, user.SecurityStamp);
        var adminBefore = await scenario.SummaryAsync("admin.ficticio");
        Assert.True((await scenario.SetRolesAsync("ventas.ficticio", Roles.Sales, Roles.Professional)).Succeeded);
        Assert.True((await scenario.SetRolesAsync("ventas.ficticio", Roles.Professional)).Succeeded);
        await scenario.Db.Entry(user).ReloadAsync();
        Assert.Equal(original.Id, user.Id); Assert.Equal(original.PasswordHash, user.PasswordHash);
        Assert.Equal(original.UserName, user.UserName); Assert.NotEqual(original.SecurityStamp, user.SecurityStamp);
        Assert.True(await scenario.Users.CheckPasswordAsync(user, SecurityApp.TestPassword));
        Assert.Equal(new[] { Roles.Professional }, (await scenario.SummaryAsync("ventas.ficticio")).Roles);
        var adminAfter = await scenario.SummaryAsync("admin.ficticio");
        Assert.Equal(adminBefore.ConcurrencyStamp, adminAfter.ConcurrencyStamp); Assert.Equal(adminBefore.Roles, adminAfter.Roles);
        var audits = await scenario.Db.AuditEntries.Where(x => x.TargetId == user.Id && x.Action.StartsWith("user.role")).ToListAsync();
        Assert.Contains(audits, x => x.Action == "user.role.added.Profesional" && x.ActorId == adminBefore.Id);
        Assert.Contains(audits, x => x.Action == "user.role.removed.AtencionVentas");
        Assert.Equal(2, audits.Count(x => x.Action == "user.roles.changed"));
    }

    [Fact]
    public async Task ConfirmationInactiveAccountsAndNoOpChangesKeepExistingAssignmentsAndSessions()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var scenario = await AccountScenario.OpenAsync(app);
        var original = await scenario.SummaryAsync("ventas.ficticio");
        var user = (await scenario.Users.FindByIdAsync(original.Id))!; var stamp = user.SecurityStamp;
        Assert.False((await scenario.Accounts.ChangeRolesAsync(original.Id, original.ConcurrencyStamp, [Roles.Professional], false)).Succeeded);
        Assert.True((await scenario.Accounts.ChangeRolesAsync(original.Id, original.ConcurrencyStamp, [Roles.Sales], true)).Succeeded);
        Assert.Equal(stamp, user.SecurityStamp);
        Assert.Equal(original.ConcurrencyStamp, (await scenario.SummaryAsync("ventas.ficticio")).ConcurrencyStamp);
        Assert.True((await scenario.Accounts.DeactivateAsync(original.Id)).Succeeded);
        Assert.False((await scenario.SetRolesAsync("ventas.ficticio", Roles.Professional)).Succeeded);
        Assert.False(await scenario.Db.AuditEntries.AnyAsync(x => x.Action == "user.roles.changed"));
    }

    [Fact]
    public async Task LastActiveAdministratorAndOwnAdministrativeAccessAreProtected()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var scenario = await AccountScenario.OpenAsync(app);
        var last = await scenario.SetRolesAsync("admin.ficticio", Roles.Professional);
        Assert.False(last.Succeeded); Assert.Contains("al menos un administrador", last.Error!);
        Assert.True((await scenario.SetRolesAsync("ventas.ficticio", Roles.Administrator, Roles.Sales)).Succeeded);
        Assert.False((await scenario.SetRolesAsync("admin.ficticio", Roles.Professional)).Succeeded);
        using var second = await AccountScenario.OpenAsync(app, "ventas.ficticio");
        Assert.True((await second.SetRolesAsync("admin.ficticio", Roles.Professional)).Succeeded);
        using var renewed = await AccountScenario.OpenAsync(app, "ventas.ficticio");
        Assert.False((await renewed.SetRolesAsync("ventas.ficticio", Roles.Sales)).Succeeded);
        Assert.True(await renewed.Users.IsInRoleAsync((await renewed.Users.FindByNameAsync("ventas.ficticio"))!, Roles.Administrator));
        // El actor antiguo conserva claims, pero el servidor vuelve a verificar su rol en la base.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scenario.SetRolesAsync("profesional.ficticio", Roles.Sales));
    }

    [Fact]
    public async Task ConcurrentEditorsCannotOverwriteAnotherAdministratorsRoleChange()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var first = await AccountScenario.OpenAsync(app);
        Assert.True((await first.SetRolesAsync("profesional.ficticio", Roles.Administrator, Roles.Professional)).Succeeded);
        using var second = await AccountScenario.OpenAsync(app, "profesional.ficticio");
        var snapshot = await first.SummaryAsync("ventas.ficticio");
        var another = await second.SummaryAsync("ventas.ficticio");
        var results = await Task.WhenAll(
            Task.Run(() => first.Accounts.ChangeRolesAsync(snapshot.Id, snapshot.ConcurrencyStamp, [Roles.Sales, Roles.Professional], true)),
            Task.Run(() => second.Accounts.ChangeRolesAsync(another.Id, another.ConcurrencyStamp, [Roles.Administrator, Roles.Sales], true)));
        Assert.Single(results, x => x.Succeeded);
        Assert.Contains("Recargá", results.Single(x => !x.Succeeded).Error!);
        using var check = await AccountScenario.OpenAsync(app);
        var saved = await check.SummaryAsync("ventas.ficticio");
        Assert.Equal(2, saved.Roles.Count);
        Assert.Equal(1, await check.Db.AuditEntries.CountAsync(x => x.TargetId == saved.Id && x.Action == "user.roles.changed"));
    }

    [Fact]
    public async Task SimultaneousMutualRevocationsCannotRemoveBothAdministrators()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var first = await AccountScenario.OpenAsync(app);
        Assert.True((await first.SetRolesAsync("profesional.ficticio", Roles.Administrator, Roles.Professional)).Succeeded);
        using var second = await AccountScenario.OpenAsync(app, "profesional.ficticio");
        var firstTarget = await first.SummaryAsync("profesional.ficticio");
        var secondTarget = await second.SummaryAsync("admin.ficticio");
        async Task<bool> RevokeAsync(AccountScenario scenario, AccountSummary target, string remaining)
        {
            try { return (await scenario.Accounts.ChangeRolesAsync(target.Id, target.ConcurrencyStamp, [remaining], true)).Succeeded; }
            catch (UnauthorizedAccessException) { return false; }
        }
        var results = await Task.WhenAll(
            Task.Run(() => RevokeAsync(first, firstTarget, Roles.Professional)),
            Task.Run(() => RevokeAsync(second, secondTarget, Roles.Sales)));
        Assert.Single(results, succeeded => succeeded);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpticaDbContext>();
        Assert.Equal(1, await (from user in db.Users join membership in db.UserRoles on user.Id equals membership.UserId
                              join role in db.Roles on membership.RoleId equals role.Id
                              where user.IsActive && role.Name == Roles.Administrator select user.Id).CountAsync());
    }

    [Theory]
    [InlineData("ventas.ficticio")]
    [InlineData("profesional.ficticio")]
    public async Task NonAdministratorsCannotAssignRolesEvenCallingTheApplicationService(string name)
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var scenario = await AccountScenario.OpenAsync(app, name);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scenario.Accounts.ChangeRolesAsync("any", "any", [Roles.Administrator], true));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scenario.Accounts.CreateAsync("intrusa.ficticia", SecurityApp.TestPassword, [Roles.Administrator]));
    }

    [Fact]
    public async Task AccountCreationAlsoRollsBackIfAuditingFails()
    {
        var failure = new FailRoleAudit();
        await using var app = new SecurityApp(configureServices: services =>
            services.AddDbContextFactory<OpticaDbContext>(options => options.AddInterceptors(failure)));
        await app.InitializeAsync();
        using var scenario = await AccountScenario.OpenAsync(app);
        failure.Enabled = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => scenario.Accounts.CreateAsync("fallida.ficticia", SecurityApp.TestPassword, [Roles.Sales, Roles.Professional]));
        failure.Enabled = false;
        using var check = await AccountScenario.OpenAsync(app);
        Assert.Null(await check.Users.FindByNameAsync("fallida.ficticia"));
        Assert.Equal(3, await check.Db.UserRoles.CountAsync());
        Assert.False(await check.Db.AuditEntries.AnyAsync(x => x.Action == "user.created"));
    }

    [Fact]
    public async Task AuditFailureRollsBackMembershipsSecurityStampAndConcurrencyStamp()
    {
        var failure = new FailRoleAudit();
        await using var app = new SecurityApp(configureServices: services =>
            services.AddDbContextFactory<OpticaDbContext>(options => options.AddInterceptors(failure)));
        await app.InitializeAsync();
        using var scenario = await AccountScenario.OpenAsync(app);
        var previous = await scenario.SummaryAsync("ventas.ficticio");
        var stamp = (await scenario.Users.FindByIdAsync(previous.Id))!.SecurityStamp;
        failure.Enabled = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => scenario.SetRolesAsync("ventas.ficticio", Roles.Professional));
        failure.Enabled = false;
        using var check = await AccountScenario.OpenAsync(app);
        var saved = await check.SummaryAsync("ventas.ficticio");
        Assert.Equal(previous.Roles, saved.Roles); Assert.Equal(previous.ConcurrencyStamp, saved.ConcurrencyStamp);
        Assert.Equal(stamp, (await check.Users.FindByIdAsync(saved.Id))!.SecurityStamp);
        Assert.False(await check.Db.AuditEntries.AnyAsync(x => x.Action.StartsWith("user.role")));
    }

    private sealed class FailRoleAudit : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && eventData.Context!.ChangeTracker.Entries<AuditEntry>()
                .Any(x => x.State == EntityState.Added && x.Entity.Action.StartsWith("user.role", StringComparison.Ordinal)))
                throw new InvalidOperationException("Fallo ficticio de auditoría.");
            return ValueTask.FromResult(result);
        }
    }
}
