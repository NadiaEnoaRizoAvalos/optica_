using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Application.Security;
using OpticaSosa.Infrastructure.Identity;
using OpticaSosa.Infrastructure.Persistence;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class CustomerMigrationTests
{
    [Fact]
    public async Task UpgradeFromPhaseTwoPreservesAccountsRolesAuditAndAttachments()
    {
        await using var app = new SecurityApp();
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var paths = services.GetRequiredService<StoragePaths>();
        Assert.Equal(Path.Combine(app.Root, "private"), paths.DataDirectory);
        var db = services.GetRequiredService<OpticaDbContext>();
        await db.GetService<IMigrator>().MigrateAsync("20261015231531_InitialIdentity");
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True((await roles.CreateAsync(new IdentityRole(Roles.Administrator))).Succeeded);
        var manager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = "previo.ficticio" };
        Assert.True((await manager.CreateAsync(user, SecurityApp.TestPassword)).Succeeded);
        Assert.True((await manager.AddToRoleAsync(user, Roles.Administrator)).Succeeded);
        var originalHash = user.PasswordHash;
        db.AuditEntries.Add(new AuditEntry { Action = "synthetic.previous", ActorId = user.Id });
        await db.SaveChangesAsync();
        var attachment = Path.Combine(paths.AttachmentsDirectory, "synthetic.txt");
        await File.WriteAllTextAsync(attachment, "synthetic attachment");
        await services.GetRequiredService<LocalProvisioning>().InitializeAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(originalHash, (await manager.FindByIdAsync(user.Id))!.PasswordHash);
        Assert.True(await manager.IsInRoleAsync((await manager.FindByIdAsync(user.Id))!, Roles.Administrator));
        Assert.True(await db.AuditEntries.AnyAsync(x => x.Action == "synthetic.previous"));
        Assert.Equal("synthetic attachment", await File.ReadAllTextAsync(attachment));
        Assert.Equal(0, await db.Customers.CountAsync());
    }
}
