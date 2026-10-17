using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Security;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Identity;

public sealed partial class AccountService(OpticaDbContext db, UserManager<ApplicationUser> users, ICurrentUser current) : IAccountService
{
    public async Task<IReadOnlyList<AccountSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        Demand(Permissions.ManageUsers);
        var accounts = await db.Users.AsNoTracking().OrderBy(x => x.UserName).ToListAsync(cancellationToken);
        var memberships = await (from membership in db.UserRoles.AsNoTracking()
                          join role in db.Roles on membership.RoleId equals role.Id
                          select new { membership.UserId, Name = role.Name! })
                          .ToListAsync(cancellationToken);
        await AddAuditAsync("users.viewed", null, cancellationToken);
        var byUser = memberships.ToLookup(x => x.UserId, x => x.Name);
        return accounts.Select(x => new AccountSummary(x.Id, x.UserName!, x.IsActive,
            byUser[x.Id].Order().ToArray(), x.ConcurrencyStamp!)).ToArray();
    }

    public async Task<AccountResult> CreateAsync(string userName, string password, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default)
    {
        Demand(Permissions.ManageUsers);
        if (!ValidRoles(roles) || string.IsNullOrWhiteSpace(userName) || userName.Length > 64 || password.Length > 256)
            return new(false, "Revisá el nombre de usuario y seleccioná uno o varios roles válidos, sin repetir.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await DemandActiveAdministratorAsync(cancellationToken);
        var user = new ApplicationUser { UserName = userName.Trim() };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded) return new(false, "No se pudo crear la cuenta. Revisá el nombre y los requisitos de contraseña.");
        if (!(await users.AddToRolesAsync(user, roles)).Succeeded)
            throw new InvalidOperationException("No se pudieron asignar los roles.");
        await AddAuditAsync("user.created", user.Id, cancellationToken);
        foreach (var role in roles.Order()) await AddAuditAsync("user.role.added." + role, user.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true);
    }

    public async Task<AccountResult> DeactivateAsync(string userId, CancellationToken cancellationToken = default)
    {
        Demand(Permissions.ManageUsers);
        if (current.Id == userId) return new(false, "No podés inactivar tu propia cuenta.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await DemandActiveAdministratorAsync(cancellationToken);
        var user = await users.FindByIdAsync(userId);
        if (user is null || !user.IsActive) return new(false, "La cuenta no está activa.");
        if (await users.IsInRoleAsync(user, Roles.Administrator))
        {
            var count = await (from account in db.Users
                               join membership in db.UserRoles on account.Id equals membership.UserId
                               join role in db.Roles on membership.RoleId equals role.Id
                               where account.IsActive && role.Name == Roles.Administrator
                               select account.Id).CountAsync(cancellationToken);
            if (count <= 1) return new(false, "Debe quedar al menos un administrador activo.");
        }
        user.IsActive = false;
        if (!(await users.UpdateSecurityStampAsync(user)).Succeeded)
            throw new InvalidOperationException("No se pudo inactivar la cuenta.");
        await AddAuditAsync("user.deactivated", user.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true);
    }

    public async Task<IReadOnlyList<AuditSummary>> ReadAuditAsync(CancellationToken cancellationToken = default)
    {
        Demand(Permissions.ReadAudit);
        await AddAuditAsync("audit.viewed", null, cancellationToken);
        return await db.AuditEntries.AsNoTracking().OrderByDescending(x => x.Id).Take(100)
            .Select(x => new AuditSummary(x.OccurredAt, x.Action, x.ActorId, x.TargetId)).ToListAsync(cancellationToken);
    }

    public async Task<AccountResult> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        if (current.Id is null) throw new UnauthorizedAccessException();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = await users.FindByIdAsync(current.Id);
        if (user is null || !user.IsActive) throw new UnauthorizedAccessException();
        if (newPassword.Length > 256 || !(await users.ChangePasswordAsync(user, currentPassword, newPassword)).Succeeded)
            return new(false, "Revisá la contraseña actual y los requisitos de la nueva.");
        await AddAuditAsync("password.changed", user.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true);
    }

    private void Demand(string permission)
    {
        if (current.Id is null || !current.HasPermission(permission)) throw new UnauthorizedAccessException();
    }

    private async Task AddAuditAsync(string action, string? targetId, CancellationToken cancellationToken)
    {
        db.AuditEntries.Add(new AuditEntry { Action = action, ActorId = current.Id, TargetId = targetId });
        await db.SaveChangesAsync(cancellationToken);
    }
}
