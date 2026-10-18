using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Inventory;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Infrastructure.Commerce;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Inventory;

public sealed partial class InventoryAdministration(IDbContextFactory<OpticaDbContext> factory, ICurrentUser current, TimeProvider clock) : IInventoryAdministration
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
    private static InventoryResult Invalid(string error) => new(false, Error: error);
    private static InventoryResult Conflict() => new(false, Error: "El inventario cambió o el identificador corresponde a otra operación; recargá los datos.", Conflict: true);
    private async Task<InventoryResult> ExecuteAsync(Guid batchId, Guid submission, Guid version, string action,
        StockMovementKind? kind, object input, Func<OpticaDbContext, StockBatch, string, string, Task<InventoryResult?>> operation, CancellationToken ct)
    {
        var actor = SalesOperations.Demand(current, Permissions.ReadStock);
        if (submission == Guid.Empty || batchId == Guid.Empty) return Invalid("Recargá el formulario del inventario.");
        var hash = StockService.Hash(new { batchId, action, input });
        await using var db = await factory.CreateDbContextAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var review = await db.StockReviews.AsNoTracking().SingleOrDefaultAsync(x => x.Id == submission, ct);
            if (review is not null) return kind is null && review.RequestHash == hash && review.ActorUserId == actor &&
                await db.StockHolds.AnyAsync(x => x.Id == review.HoldId && x.BatchId == batchId, ct) ? new(true, submission) : Conflict();
            var prior = await db.StockMovements.AsNoTracking().SingleOrDefaultAsync(x => x.Id == submission, ct);
            if (prior is not null) return kind == prior.Kind && prior.BatchId == batchId && prior.RequestHash == hash && prior.ActorUserId == actor
                ? new(true, submission) : Conflict();
            var batch = await db.StockBatches.SingleOrDefaultAsync(x => x.Id == batchId, ct);
            if (batch is null) return Invalid("No se encontró el lote de inventario.");
            if (batch.Version != version) return Conflict();
            var result = await operation(db, batch, actor, hash);
            if (result is not null) return result;
            db.AuditEntries.Add(new() { ActorId = actor, Action = action, TargetId = batchId.ToString(), OccurredAt = clock.GetUtcNow() });
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return new(true, submission);
        }
        catch (ArgumentException ex) { return Invalid(ex.Message); }
        catch (OverflowException) { return Invalid("La cantidad excede la capacidad del inventario."); }
        catch (DbUpdateConcurrencyException) { return Conflict(); }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 or 5 or 6 }) { return Conflict(); }
        catch (SqliteException ex) when (ex.SqliteErrorCode is 5 or 6) { return Conflict(); }
    }
    private void Movement(OpticaDbContext db, Guid id, StockBatch batch, Guid? reservation, StockMovementKind kind,
        int physical, int quarantine, string reason, string hash, string actor) => db.StockMovements.Add(new(id, batch.Id,
            reservation, kind, physical, 0, quarantine, reason, hash, actor, clock.GetUtcNow().UtcDateTime));
    private async Task<StockHold> HoldAsync(OpticaDbContext db, StockBatch batch, Guid? holdId, Guid? version, int quantity, CancellationToken ct)
    {
        var hold = await db.StockHolds.SingleOrDefaultAsync(x => x.Id == holdId && x.BatchId == batch.Id, ct);
        if (hold is not null && hold.Version != version) throw new DbUpdateConcurrencyException("La cuarentena cambió.");
        if (hold is null || quantity <= 0 || quantity > hold.PendingQuantity)
            throw new ArgumentException("Revisá cantidad y versión del ingreso a cuarentena.");
        return hold;
    }
}
