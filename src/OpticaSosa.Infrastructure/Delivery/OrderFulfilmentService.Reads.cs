using System.Data;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Delivery;
using OpticaSosa.Domain.Commerce;
using OpticaSosa.Domain.Delivery;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Domain.Work;
using OpticaSosa.Infrastructure.Commerce;

namespace OpticaSosa.Infrastructure.Delivery;

public sealed partial class OrderFulfilmentService
{
    public async Task<FulfilmentSummary?> GetAsync(Guid orderId, CancellationToken ct = default)
    {
        Read(); await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await db.SalesOrders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == orderId, ct);
        if (order is null) return null;
        var delivery = await db.Set<OrderDelivery>().AsNoTracking().SingleOrDefaultAsync(x => x.OrderId == orderId, ct);
        var state = order.Status == SalesOrderStatus.Cancelled ? FulfilmentState.Cancelled : FulfilmentState.NotGrouped;
        if (delivery is not null) state = FulfilmentState.Delivered;
        else if (order.Status == SalesOrderStatus.Confirmed)
        {
            var count = await db.SalesLines.CountAsync(x => x.OrderId == orderId && x.DraftNumber == order.DraftNumber, ct);
            var bound = await db.Set<CommercialWorkLine>().CountAsync(x => x.IsCurrent && db.Set<CommercialWork>().Any(g => g.WorkId == x.WorkId && g.OrderId == orderId && g.IsCurrent), ct);
            if (count > 0 && bound == count)
            {
                var groups = await db.Set<CommercialWork>().AsNoTracking().Where(x => x.OrderId == orderId && x.IsCurrent).ToListAsync(ct);
                state = FulfilmentState.Ready;
                var allReady = true; var preparing = false;
                foreach (var group in groups)
                {
                    var work = await db.OpticalWorks.AsNoTracking().SingleAsync(x => x.Id == group.WorkId, ct);
                    var readiness = await CommercialWorkRules.CheckAsync(db, group, clock, false, ct);
                    if (!readiness.Ready) { state = FulfilmentState.WaitingMaterials; break; }
                    preparing |= work.Status is WorkStatus.Registered or WorkStatus.InPreparation;
                    allReady &= work.Status == WorkStatus.ReadyForDelivery && group.TechnicalMaterialHash == readiness.MaterialHash && group.DeliveryMaterialHash == readiness.MaterialHash;
                }
                if (state != FulfilmentState.WaitingMaterials)
                    state = allReady && order.BalanceMinor == 0 ? FulfilmentState.Ready : preparing ? FulfilmentState.Preparing : FulfilmentState.UnderReview;
            }
        }
        return new(order.Id, order.CustomerId, order.Version, state, order.BalanceMinor == 0, delivery?.ActorUserId, delivery?.DeliveredAtUtc);
    }
    public async Task<FulfilmentPage<FulfilmentMaterial>> MaterialsAsync(Guid orderId, int page = 1, CancellationToken ct = default)
    {
        Read(); var skip = SalesOperations.Skip(page); await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await db.SalesLines.AsNoTracking().Where(x => x.OrderId == orderId && db.SalesOrders.Any(o => o.Id == orderId && o.DraftNumber == x.DraftNumber))
            .OrderBy(x => x.Position).Skip(skip).Take(51).ToListAsync(ct);
        var result = new List<FulfilmentMaterial>();
        foreach (var line in rows.Take(50))
        {
            var binding = await db.Set<CommercialWorkLine>().AsNoTracking().SingleOrDefaultAsync(x => x.LineId == line.Id && x.IsCurrent, ct);
            var assignment = await db.OrderTechnicalAssignments.AsNoTracking().Where(x => x.LineId == line.Id && x.IsCurrent).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            var reserved = await db.StockReservations.Where(x => x.LineId == line.Id && x.Status == ReservationStatus.Active).SumAsync(x => (long)x.Quantity, ct);
            result.Add(new(line.Id, line.VariantId, line.ProductName, line.VariantCode, line.Category, line.Quantity, assignment, binding?.WorkId, reserved));
        }
        return new(result, page, rows.Count > 50);
    }
    public async Task<FulfilmentPage<FulfilmentWork>> WorksAsync(Guid orderId, bool includeHistory = false, int page = 1, CancellationToken ct = default)
    {
        Read(); var skip = SalesOperations.Skip(page); await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var rows = await (from entry in db.Set<CommercialWork>().AsNoTracking() join work in db.OpticalWorks.AsNoTracking() on entry.WorkId equals work.Id
            where entry.OrderId == orderId && (includeHistory || entry.IsCurrent) orderby work.CreatedAtUtc, work.Id select new { Group = entry, Work = work })
            .Skip(skip).Take(51).ToListAsync(ct);
        var result = new List<FulfilmentWork>();
        foreach (var row in rows.Take(50))
        {
            if (row.Work.Status == WorkStatus.Delivered)
            {
                result.Add(new(row.Work.Id, row.Work.Version, row.Work.Type, row.Work.Status, row.Work.Round, row.Group.IsCurrent, true, true, null,
                    row.Group.PreviousWorkId, row.Group.ReplacementReason));
                continue;
            }
            var readiness = await CommercialWorkRules.CheckAsync(db, row.Group, clock, false, ct);
            result.Add(new(row.Work.Id, row.Work.Version, row.Work.Type, row.Work.Status, row.Work.Round, row.Group.IsCurrent, readiness.Ready,
                readiness.Ready && row.Group.TechnicalMaterialHash == readiness.MaterialHash && row.Group.DeliveryMaterialHash == readiness.MaterialHash,
                readiness.Error, row.Group.PreviousWorkId, row.Group.ReplacementReason));
        }
        return new(result, page, rows.Count > 50);
    }
}
