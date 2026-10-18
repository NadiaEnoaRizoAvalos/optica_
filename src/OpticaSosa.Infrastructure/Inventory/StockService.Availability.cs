using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Inventory;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Commerce;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Domain.Technical;
using OpticaSosa.Infrastructure.Commerce;

namespace OpticaSosa.Infrastructure.Inventory;

public sealed partial class StockService
{
    public async Task<StockMovementPage> MovementsAsync(Guid batchId, int page = 1, CancellationToken ct = default)
    {
        SalesOperations.Demand(current, Permissions.ReadStock);
        var skip = SalesOperations.Skip(page);
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await db.StockMovements.AsNoTracking().Where(x => x.BatchId == batchId)
            .OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.Id).Skip(skip).Take(51).ToListAsync(ct);
        return new(rows.Take(50).Select(x => new StockMovementView(x.Id, x.Kind, x.ReservationId, x.PhysicalDelta,
            x.ReservedDelta, x.QuarantineDelta, x.Reason, x.ActorUserId, x.OccurredAtUtc)).ToArray(), page, rows.Count > 50);
    }
    public async Task<OrderAvailabilityPage> AvailabilityAsync(Guid orderId, int page = 1, CancellationToken ct = default)
    {
        SalesOperations.Demand(current, Permissions.ReadSales);
        var skip = SalesOperations.Skip(page);
        await using var db = await factory.CreateDbContextAsync(ct);
        var lines = await db.SalesLines.AsNoTracking().Where(x => x.OrderId == orderId &&
            db.SalesOrders.Any(o => o.Id == orderId && o.DraftNumber == x.DraftNumber))
            .OrderBy(x => x.Position).Skip(skip).Take(51).ToListAsync(ct);
        var selected = lines.Take(50).ToArray(); var ids = selected.Select(x => x.Id).ToArray();
        var variants = selected.Select(x => x.VariantId).Distinct().ToArray(); var today = Today;
        var availability = await db.StockBatches.AsNoTracking().Where(x => variants.Contains(x.VariantId) && (x.ExpiresOn == null || x.ExpiresOn >= today) &&
            db.ProductVariants.Any(v => v.Id == x.VariantId && v.IsActive && db.Products.Any(p => p.Id == v.ProductId && p.IsActive)))
            .GroupBy(x => x.VariantId).Select(g => new { Id = g.Key, Available = g.Sum(x => (long)x.OnHand - x.Reserved - x.Quarantined) }).ToDictionaryAsync(x => x.Id, x => x.Available, ct);
        var reserved = await db.StockReservations.AsNoTracking().Where(x => ids.Contains(x.LineId) && x.Status != ReservationStatus.Released)
            .GroupBy(x => x.LineId).Select(g => new { Id = g.Key, Quantity = g.Sum(x => (long)x.Quantity) }).ToDictionaryAsync(x => x.Id, x => x.Quantity, ct);
        var assignments = await db.OrderTechnicalAssignments.AsNoTracking().Where(x => ids.Contains(x.LineId) && x.IsCurrent).ToListAsync(ct);
        var result = new List<OrderAvailabilityLine>();
        foreach (var line in selected)
        {
            var used = reserved.GetValueOrDefault(line.Id); var pending = line.Quantity - used;
            var available = line.Source == FulfilmentSource.Stock ? availability.GetValueOrDefault(line.VariantId) : 0;
            var required = OrderTechnicalRules.Required(line.Category); var valid = !required;
            var assignment = assignments.SingleOrDefault(x => x.LineId == line.Id);
            if (assignment is not null) valid = (await OrderTechnicalRules.LoadAsync(db, assignment.PublicationId, ct))?.Status == PublicationStatus.Current;
            result.Add(new(line.Id, line.VariantId, line.Source, line.Quantity, used, pending, available, Math.Max(0, pending - available), required, valid));
        }
        return new(result, page, lines.Count > 50);
    }
}
