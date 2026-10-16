using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Security;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Identity;

// Invocado sólo por los comandos locales; nunca se publica como endpoint HTTP.
public sealed class LocalProvisioning(OpticaDbContext db, UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles)
{
    public async Task InitializeAsync()
    {
        await db.Database.MigrateAsync();
        foreach (var role in Roles.All)
            if (!await roles.RoleExistsAsync(role) && !(await roles.CreateAsync(new IdentityRole(role))).Succeeded)
                throw new InvalidOperationException("No se pudieron inicializar los roles.");
    }

    public async Task<AccountResult> CreateFirstAdminAsync(string userName, string password)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (await db.Users.AnyAsync()) return new(false, "Ya existen usuarios; usá la administración de cuentas.");
        if (string.IsNullOrWhiteSpace(userName) || userName.Length > 64) return new(false, "Nombre inválido.");
        var user = new ApplicationUser { UserName = userName.Trim() };
        if (!(await users.CreateAsync(user, password)).Succeeded) return new(false, "Nombre o contraseña inválidos.");
        if (!(await users.AddToRoleAsync(user, Roles.Administrator)).Succeeded)
            throw new InvalidOperationException("No se pudo asignar el administrador.");
        db.AuditEntries.Add(new AuditEntry { Action = "admin.provisioned", ActorId = "local-console", TargetId = user.Id });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new(true);
    }

    public async Task<AccountResult> RecoverAdminAsync(string userName, string password)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = await users.FindByNameAsync(userName);
        if (user is null || !user.IsActive || !await users.IsInRoleAsync(user, Roles.Administrator))
            return new(false, "No se encontró un administrador activo.");
        var token = await users.GeneratePasswordResetTokenAsync(user);
        if (!(await users.ResetPasswordAsync(user, token, password)).Succeeded)
            return new(false, "La contraseña no cumple los requisitos.");
        if (!(await users.SetLockoutEndDateAsync(user, null)).Succeeded ||
            !(await users.ResetAccessFailedCountAsync(user)).Succeeded)
            throw new InvalidOperationException("No se pudo restablecer el acceso.");
        db.AuditEntries.Add(new AuditEntry { Action = "admin.recovered", ActorId = "local-console", TargetId = user.Id });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new(true);
    }
}
