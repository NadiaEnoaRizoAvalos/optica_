using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Inventory;
using OpticaSosa.Application.Security;
using OpticaSosa.Infrastructure.Commerce;

namespace OpticaSosa.Infrastructure.Inventory;

public sealed partial class InventoryAdministration
{
    public async Task<StockHoldPage> HoldsAsync(Guid? batchId = null, bool includeClosed = false, int page = 1, CancellationToken ct = default)
    {
        SalesOperations.Demand(current, Permissions.ReadStock); var skip = SalesOperations.Skip(page);
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await (from hold in db.StockHolds.AsNoTracking() join batch in db.StockBatches.AsNoTracking() on hold.BatchId equals batch.Id
            where (batchId == null || batch.Id == batchId) && (includeClosed || hold.Quantity - hold.ReleasedQuantity - hold.RemovedQuantity > 0)
            orderby hold.CreatedAtUtc descending, hold.Id descending
            select new { Hold = hold, batch.Category }).Skip(skip).Take(51).ToListAsync(ct);
        return new(rows.Take(50).Select(x => new StockHoldView(x.Hold.Id, x.Hold.BatchId, x.Category, x.Hold.Origin, x.Hold.Quantity,
            x.Hold.ReleasedQuantity, x.Hold.RemovedQuantity, x.Hold.PendingQuantity, x.Hold.OpenedContact, x.Hold.Reason,
            x.Hold.CreatedByUserId, x.Hold.CreatedAtUtc, x.Hold.Version)).ToArray(), page, rows.Count > 50);
    }
    public async Task<StockReviewPage> ReviewsAsync(Guid holdId, int page = 1, CancellationToken ct = default)
    {
        SalesOperations.Demand(current, Permissions.ReadStock); var skip = SalesOperations.Skip(page);
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await db.StockReviews.AsNoTracking().Where(x => x.HoldId == holdId)
            .OrderByDescending(x => x.ReviewedAtUtc).ThenByDescending(x => x.Id).Skip(skip).Take(51).ToListAsync(ct);
        return new(rows.Take(50).Select(x => new StockReviewView(x.Id, x.HoldId, x.Quantity, x.Approved, x.ProductMatches,
            x.ItemIntact, x.SealIntact, x.Reason, x.ActorUserId, x.Role, x.ReviewedAtUtc)).ToArray(), page, rows.Count > 50);
    }
    public async Task<StockReturnPage> ReturnsAsync(Guid reservationId, int page = 1, CancellationToken ct = default)
    {
        SalesOperations.Demand(current, Permissions.ReadSales); var skip = SalesOperations.Skip(page);
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await db.StockCustomerReturns.AsNoTracking().Where(x => x.ReservationId == reservationId)
            .OrderByDescending(x => x.ReturnedAtUtc).ThenByDescending(x => x.Id).Skip(skip).Take(51).ToListAsync(ct);
        return new(rows.Take(50).Select(x => new StockReturnView(x.Id, x.ReservationId, x.HoldId, x.Quantity,
            x.ContactPackageOpened, x.Reason, x.ActorUserId, x.ReturnedAtUtc)).ToArray(), page, rows.Count > 50);
    }
    public async Task<StockReconciliation?> ReconciliationAsync(Guid batchId, CancellationToken ct = default)
    {
        SalesOperations.Demand(current, Permissions.ReadStock);
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var batch = await db.StockBatches.AsNoTracking().SingleOrDefaultAsync(x => x.Id == batchId, ct);
        if (batch is null) return null;
        var movements = db.StockMovements.Where(x => x.BatchId == batchId);
        return new(batch.Id, batch.OnHand, batch.Reserved, batch.Quarantined, await movements.SumAsync(x => (long)x.PhysicalDelta, ct),
            await movements.SumAsync(x => (long)x.ReservedDelta, ct), await movements.SumAsync(x => (long)x.QuarantineDelta, ct),
            await db.StockHolds.Where(x => x.BatchId == batchId).SumAsync(x => (long)x.Quantity - x.ReleasedQuantity - x.RemovedQuantity, ct));
    }
}
