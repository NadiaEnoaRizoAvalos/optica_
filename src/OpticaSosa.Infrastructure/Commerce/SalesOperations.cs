using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Commerce;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Commerce;
using OpticaSosa.Infrastructure.Identity;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Commerce;

internal static class SalesOperations
{
    internal static string Demand(ICurrentUser user, string permission) => user.Id is { Length: > 0 } actor && user.HasPermission(permission)
        ? actor : throw new UnauthorizedAccessException();
    internal static SalesResult Invalid(string error) => new(SalesResultCode.Invalid, Error: error);
    internal static SalesResult Missing() => new(SalesResultCode.NotFound, Error: "No se encontró el pedido comercial.");
    internal static SalesResult Conflict() => new(SalesResultCode.Conflict, Error: "El pedido cambió o el identificador ya se usó para otra operación; recargá los datos.");
    internal static int Skip(int page)
    {
        if (page < 1 || page > int.MaxValue / 50) throw new ArgumentOutOfRangeException(nameof(page));
        return (page - 1) * 50;
    }
    // Una transacción SQLite serializa la validación y escritura; Version también rechaza formularios anteriores.
    internal static async Task<SalesResult> ExecuteAsync(IDbContextFactory<OpticaDbContext> factory, TimeProvider clock,
        string actor, Guid orderId, Guid submissionId, string action, object request,
        Func<OpticaDbContext, Task<SalesResult?>> operation, CancellationToken ct)
    {
        if (orderId == Guid.Empty || submissionId == Guid.Empty) return Invalid("Recargá el formulario de la operación.");
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));
        await using var db = await factory.CreateDbContextAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var prior = await db.OrderCommandReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == submissionId, ct);
            if (prior is not null) return prior.OrderId == orderId && prior.Action == action && prior.RequestHash == hash && prior.ActorUserId == actor
                ? new(SalesResultCode.Success, orderId) : Conflict();
            var result = await operation(db);
            if (result is not null) return result;
            var now = clock.GetUtcNow().UtcDateTime;
            db.OrderCommandReceipts.Add(new(submissionId, orderId, action, hash, actor, now));
            db.AuditEntries.Add(new AuditEntry { ActorId = actor, Action = action, TargetId = orderId.ToString(), OccurredAt = clock.GetUtcNow() });
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return new(SalesResultCode.Success, orderId);
        }
        catch (ArgumentException ex) { return Invalid(ex.Message); }
        catch (OverflowException) { return Invalid("Los importes o cantidades exceden la capacidad del sistema."); }
        catch (DbUpdateConcurrencyException) { return Conflict(); }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 or 5 or 6 }) { return Conflict(); }
        catch (SqliteException ex) when (ex.SqliteErrorCode is 5 or 6) { return Conflict(); }
    }
}
