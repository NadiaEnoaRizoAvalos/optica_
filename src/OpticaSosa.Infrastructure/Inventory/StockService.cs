using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Inventory;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Domain.Catalog;
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
        var rows = await QueryBatches(db).Where(x => variantId == null || x.Batch.VariantId == variantId)
            .OrderBy(x => x.Batch.ExpiresOn == null).ThenBy(x => x.Batch.ExpiresOn).ThenBy(x => x.Batch.ReceivedAtUtc).ThenBy(x => x.Batch.Id)
            .Skip(skip).Take(51).ToListAsync(ct);
        var today = Today;
        return new(rows.Take(50).Select(x => BatchView(x, today)).ToArray(), page, rows.Count > 50);
    }
    private sealed record BatchRow
    {
        public required StockBatch Batch { get; init; }
        public required ProductVariant Variant { get; init; }
        public required Product Product { get; init; }
    }
    private static IQueryable<BatchRow> QueryBatches(OpticaDbContext db) =>
        from batch in db.StockBatches.AsNoTracking() join variant in db.ProductVariants.AsNoTracking() on batch.VariantId equals variant.Id
        join product in db.Products.AsNoTracking() on variant.ProductId equals product.Id select new BatchRow { Batch = batch, Variant = variant, Product = product };
    private static StockBatchView BatchView(BatchRow row, DateOnly today)
    {
        var b = row.Batch; var v = row.Variant; var p = row.Product; var active = v.IsActive && p.IsActive;
        return new(b.Id, b.VariantId, b.Category, b.Lot, b.ExpiresOn, b.ExpiryPrecision, b.OnHand, b.Reserved, b.Quarantined,
            active ? b.Available(today) : 0, b.Version, b.ReceivedAtUtc, p.Id, p.Name, v.Code, v.Unit, v.UnitsPerPackage, active, b.ExpiresOn is { } expiry && today > expiry);
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
