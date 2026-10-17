using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Security;
using OpticaSosa.Application.Viewer;
using OpticaSosa.Infrastructure.Identity;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Viewer;

public sealed class ViewerAccounts(OpticaDbContext db, UserManager<ApplicationUser> users,
    ICurrentUser current, ViewerAccess access, ViewerJournal audit) : IViewerAccounts
{
    public async Task<IReadOnlyList<ViewerAccount>> ListAsync(CancellationToken ct = default)
    {
        await DemandAsync();
        var accounts = await db.Users.AsNoTracking().OrderBy(x => x.UserName).ToListAsync(ct);
        var rows = await (from membership in db.UserRoles join role in db.Roles on membership.RoleId equals role.Id
                          select new { membership.UserId, Name = role.Name! }).ToListAsync(ct);
        var roles = rows.ToLookup(x => x.UserId, x => x.Name);
        await audit.RecordAsync("viewer.access.listed", current.Id, ct: ct);
        return accounts.Select(x => new ViewerAccount(new(x.Id, x.UserName!, x.IsActive,
            roles[x.Id].Order().ToArray(), x.ConcurrencyStamp!), access.IsRevoked(x.Id))).ToArray();
    }

    public async Task<AccountResult> RevokeAsync(string? target, bool confirmed, CancellationToken ct = default)
    {
        await DemandAsync();
        if (!confirmed || string.IsNullOrWhiteSpace(target)) return new(false, "Seleccioná la cuenta y confirmá la revocación local.");
        if (target == current.Id) return new(false, "No podés revocar tu propio acceso administrativo.");
        if (await users.FindByIdAsync(target) is null) return new(false, "La cuenta no existe en esta copia.");
        await access.RevokeAsync(current.Id!, target, ct);
        return new(true);
    }

    private async Task DemandAsync()
    {
        if (current.Id is null || !current.HasPermission(Permissions.ManageUsers) || access.IsRevoked(current.Id))
            throw new UnauthorizedAccessException();
        var actor = await users.FindByIdAsync(current.Id);
        if (actor is null || !actor.IsActive || !await users.IsInRoleAsync(actor, Roles.Administrator))
            throw new UnauthorizedAccessException();
    }
}
