using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Commerce;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Commerce;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Commerce;

public sealed partial class SalesOrderService(IDbContextFactory<OpticaDbContext> factory, ICurrentUser current, TimeProvider clock)
    : ISalesOrderService
{
    public async Task<SalesOrderPage> ListAsync(Guid? customerId = null, int page = 1, CancellationToken ct = default)
    {
        SalesOperations.Demand(current, Permissions.ReadSales);
        var skip = SalesOperations.Skip(page);
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await db.SalesOrders.AsNoTracking().Where(x => customerId == null || x.CustomerId == customerId)
            .OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).Skip(skip).Take(51).ToListAsync(ct);
        return new(rows.Take(50).Select(Summary).ToArray(), page, rows.Count > 50);
    }
    public async Task<SalesOrderSummary?> GetAsync(Guid orderId, CancellationToken ct = default)
    {
        SalesOperations.Demand(current, Permissions.ReadSales);
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.SalesOrders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == orderId, ct);
        return row is null ? null : Summary(row);
    }
    public async Task<SalesLinePage?> LinesAsync(Guid orderId, int page = 1, int? draftNumber = null, CancellationToken ct = default)
    {
        SalesOperations.Demand(current, Permissions.ReadSales);
        var skip = SalesOperations.Skip(page);
        await using var db = await factory.CreateDbContextAsync(ct);
        var order = await db.SalesOrders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == orderId, ct);
        if (order is null) return null;
        var revision = draftNumber ?? order.DraftNumber;
        if (revision < 1 || revision > order.DraftNumber) throw new ArgumentOutOfRangeException(nameof(draftNumber));
        var rows = await db.SalesLines.AsNoTracking().Where(x => x.OrderId == orderId && x.DraftNumber == revision)
            .OrderBy(x => x.Position).Skip(skip).Take(51).ToListAsync(ct);
        return new(rows.Take(50).Select(x => new SalesLineView(x.Id, x.VariantId, x.ProductName, x.VariantCode, x.Category,
            x.Unit, x.UnitsPerPackage, x.Quantity, x.Source, x.UnitPriceMinor, x.DiscountMinor, x.DiscountReason, x.TotalMinor, x.Position)).ToArray(), revision, page, rows.Count > 50);
    }
    private static SalesOrderSummary Summary(SalesOrder x) => new(x.Id, x.CustomerId, x.Status, x.Currency, x.TotalMinor,
        x.ReceivedMinor, x.RefundedMinor, x.PromisedDate, x.DraftNumber, x.Version, x.CreatedAtUtc, x.ConfirmedAtUtc, x.CancelledAtUtc);
}
