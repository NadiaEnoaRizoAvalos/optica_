using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Security;
using OpticaSosa.Infrastructure.Identity;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Clinical;

internal static class ClinicalOperations
{
    internal static string Demand(ICurrentUser current, string permission)
    {
        if (current.Id is null || !current.HasPermission(permission)) throw new UnauthorizedAccessException();
        return current.Id;
    }
    internal static void Audit(OpticaDbContext db, string actor, string action, Guid? target) =>
        db.AuditEntries.Add(new AuditEntry { ActorId = actor, Action = action, TargetId = target?.ToString() });
    internal static ClinicalResult Conflict() => new(ClinicalResultCode.Conflict,
        Error: "Los datos cambiaron o ya existe ese registro. Recargá la ficha antes de guardar.");
    internal static ClinicalResult Invalid(string message) => new(ClinicalResultCode.Invalid, Error: message);
    internal static ClinicalResult Missing() => new(ClinicalResultCode.NotFound, Error: "No se encontró el registro para este cliente.");
    internal static async Task<ClinicalResult> SaveAsync(OpticaDbContext db, Guid id, CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return new(ClinicalResultCode.Success, id); }
        catch (DbUpdateConcurrencyException) { return Conflict(); }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 or 5 or 6 })
        { return Conflict(); }
        catch (SqliteException ex) when (ex.SqliteErrorCode is 5 or 6) { return Conflict(); }
    }
}
