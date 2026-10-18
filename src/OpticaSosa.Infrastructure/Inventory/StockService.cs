using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Inventory;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Infrastructure.Commerce;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Inventory;

public sealed partial class StockService(IDbContextFactory<OpticaDbContext> factory, ICurrentUser current, TimeProvider clock) : IStockService
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
    public async Task<StockBatchPage> BatchesAsync(Guid? variantId = null, int page = 1, CancellationToken ct = default)
    {
        SalesOperations.Demand(current, Permissions.ReadStock);
        var skip = SalesOperations.Skip(page);
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await db.StockBatches.AsNoTracking().Where(x => variantId == null || x.VariantId == variantId)
            .OrderBy(x => x.ExpiresOn == null).ThenBy(x => x.ExpiresOn).ThenBy(x => x.ReceivedAtUtc).ThenBy(x => x.Id)
            .Skip(skip).Take(51).ToListAsync(ct);
        var today = Today;
        return new(rows.Take(50).Select(x => new StockBatchView(x.Id, x.VariantId, x.Category, x.Lot, x.ExpiresOn, x.ExpiryPrecision,
            x.OnHand, x.Reserved, x.Quarantined, x.Available(today), x.Version, x.ReceivedAtUtc)).ToArray(), page, rows.Count > 50);
    }
    public async Task<StockReservationPage> ReservationsAsync(Guid orderId, int page = 1, CancellationToken ct = default)
    {
        SalesOperations.Demand(current, Permissions.ReadSales);
        var skip = SalesOperations.Skip(page);
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await db.StockReservations.AsNoTracking().Where(x => db.SalesLines.Any(l => l.Id == x.LineId && l.OrderId == orderId))
            .OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).Skip(skip).Take(51).ToListAsync(ct);
        return new(rows.Take(50).Select(x => new StockReservationView(x.Id, x.LineId, x.BatchId, x.TechnicalAssignmentId, x.Quantity,
            x.Status, x.Version, x.CreatedAtUtc, x.ClosedAtUtc, x.ClosureReason)).ToArray(), page, rows.Count > 50);
    }
}
