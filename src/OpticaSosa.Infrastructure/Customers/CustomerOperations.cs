using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Customers;
using OpticaSosa.Application.Security;
using OpticaSosa.Infrastructure.Identity;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Customers;

internal static class CustomerOperations
{
    internal static string Demand(ICurrentUser current, string permission)
    {
        if (current.Id is null || !current.HasPermission(permission)) throw new UnauthorizedAccessException();
        return current.Id;
    }

    internal static void Audit(OpticaDbContext db, string actorId, string action, Guid? target) =>
        db.AuditEntries.Add(new AuditEntry { ActorId = actorId, Action = action, TargetId = target?.ToString() });

    internal static async Task<CustomerResult> SaveAsync(OpticaDbContext db, Guid id, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return new(CustomerResultCode.Success, id);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 })
        {
            return new(CustomerResultCode.Conflict, Error: "No se pudo guardar: otra operación cambió los datos o existe un registro duplicado. Recargá la ficha.");
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode is 5 or 6)
        {
            return new(CustomerResultCode.Conflict, Error: "Los datos están ocupados. Volvé a intentar desde la ficha actualizada.");
        }
    }

    internal static CustomerResult Conflict() => new(CustomerResultCode.Conflict,
        Error: "Los datos cambiaron desde que abriste la pantalla. Recargá la ficha antes de guardar.");
    internal static CustomerResult Missing() => new(CustomerResultCode.NotFound, Error: "El cliente no existe.");
}
