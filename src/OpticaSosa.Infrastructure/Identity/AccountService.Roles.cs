using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Security;

namespace OpticaSosa.Infrastructure.Identity;

public sealed partial class AccountService
{
    public async Task<AccountResult> ChangeRolesAsync(string userId, string concurrencyStamp,
        IReadOnlyCollection<string> roles, bool confirmed, CancellationToken cancellationToken = default)
    {
        Demand(Permissions.ManageUsers);
        if (!confirmed) return new(false, "Confirmá el cambio de permisos antes de guardar.");
        if (!ValidRoles(roles)) return new(false, "Seleccioná al menos un rol válido, sin repetir.");
        if (string.IsNullOrWhiteSpace(userId)) return new(false, "La cuenta no existe.");

        // SQLite serializa los escritores: la comprobación del último administrador y
        // las asignaciones/sellos/auditoría forman una sola transacción.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await DemandActiveAdministratorAsync(cancellationToken);
        var user = await users.FindByIdAsync(userId);
        if (user is null) return new(false, "La cuenta no existe.");
        await db.Entry(user).ReloadAsync(cancellationToken);
        if (!user.IsActive) return new(false, "La cuenta no está activa.");
        if (string.IsNullOrEmpty(concurrencyStamp) || user.ConcurrencyStamp != concurrencyStamp)
            return new(false, "La cuenta cambió. Recargá la pantalla y revisá sus roles antes de guardar.");

        var previous = await users.GetRolesAsync(user);
        var removed = previous.Except(roles).Order().ToArray();
        var added = roles.Except(previous).Order().ToArray();
        if (removed.Contains(Roles.Administrator))
        {
            var administrators = await (from account in db.Users
                                        join membership in db.UserRoles on account.Id equals membership.UserId
                                        join role in db.Roles on membership.RoleId equals role.Id
                                        where account.IsActive && role.Name == Roles.Administrator
                                        select account.Id).CountAsync(cancellationToken);
            if (administrators <= 1) return new(false, "Debe quedar al menos un administrador activo.");
            if (userId == current.Id) return new(false, "No podés quitarte tu propio acceso administrativo.");
        }
        if (removed.Length == 0 && added.Length == 0) return new(true);

        try
        {
            if (removed.Length > 0 && !(await users.RemoveFromRolesAsync(user, removed)).Succeeded ||
                added.Length > 0 && !(await users.AddToRolesAsync(user, added)).Succeeded ||
                !(await users.UpdateSecurityStampAsync(user)).Succeeded)
                throw new InvalidOperationException("No se pudieron actualizar los roles. No se guardaron cambios.");

            foreach (var role in removed) await AddAuditAsync("user.role.removed." + role, userId, cancellationToken);
            foreach (var role in added) await AddAuditAsync("user.role.added." + role, userId, cancellationToken);
            await AddAuditAsync("user.roles.changed", userId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(true);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            // No reutilizar estados de Identity modificados que no llegaron a persistirse.
            db.ChangeTracker.Clear();
            throw;
        }
    }

    private static bool ValidRoles(IReadOnlyCollection<string> roles) => roles.Count > 0 &&
        roles.Count <= Roles.All.Count && roles.Distinct(StringComparer.Ordinal).Count() == roles.Count &&
        roles.All(role => Roles.All.Contains(role));

    private async Task DemandActiveAdministratorAsync(CancellationToken cancellationToken)
    {
        var allowed = await (from account in db.Users.AsNoTracking()
                             join membership in db.UserRoles on account.Id equals membership.UserId
                             join role in db.Roles on membership.RoleId equals role.Id
                             where account.Id == current.Id && account.IsActive && role.Name == Roles.Administrator
                             select account.Id).AnyAsync(cancellationToken);
        if (!allowed) throw new UnauthorizedAccessException();
    }
}
