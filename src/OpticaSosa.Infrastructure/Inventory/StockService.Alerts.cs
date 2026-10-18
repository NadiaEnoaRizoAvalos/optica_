using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Inventory;
using OpticaSosa.Application.Security;
using OpticaSosa.Infrastructure.Commerce;

namespace OpticaSosa.Infrastructure.Inventory;

public sealed partial class StockService
{
    public async Task<StockBatchView?> BatchAsync(Guid batchId, CancellationToken ct = default)
    {
        SalesOperations.Demand(current, Permissions.ReadStock);
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await QueryBatches(db).SingleOrDefaultAsync(x => x.Batch.Id == batchId, ct);
        return row is null ? null : BatchView(row, Today);
    }
    public async Task<StockBatchPage> AlertsAsync(DateOnly through, int page = 1, CancellationToken ct = default)
    {
        SalesOperations.Demand(current, Permissions.ReadStock); var skip = SalesOperations.Skip(page);
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await QueryBatches(db).Where(x => x.Batch.OnHand > 0 &&
            (x.Batch.Quarantined > 0 || (x.Batch.ExpiresOn != null && x.Batch.ExpiresOn <= through) || !x.Variant.IsActive || !x.Product.IsActive))
            .OrderBy(x => x.Batch.ExpiresOn).ThenBy(x => x.Batch.Id).Skip(skip).Take(51).ToListAsync(ct);
        var today = Today;
        return new(rows.Take(50).Select(x => BatchView(x, today)).ToArray(), page, rows.Count > 50);
    }
}
