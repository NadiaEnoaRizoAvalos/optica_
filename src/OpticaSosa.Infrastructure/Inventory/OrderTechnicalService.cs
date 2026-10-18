using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Commerce;
using OpticaSosa.Application.Inventory;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Clinical;
using OpticaSosa.Domain.Commerce;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Domain.Technical;
using OpticaSosa.Infrastructure.Commerce;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Inventory;

public sealed partial class OrderTechnicalService(IDbContextFactory<OpticaDbContext> factory, ICurrentUser current, TimeProvider clock) : IOrderTechnicalService
{
    public async Task<TechnicalOrderLinePage> LinesForProfessionalAsync(Guid? customerId = null, int page = 1, CancellationToken ct = default)
    {
        SalesOperations.Demand(current, Permissions.ReadClinical);
        var skip = SalesOperations.Skip(page);
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await (from line in db.SalesLines.AsNoTracking() join order in db.SalesOrders.AsNoTracking() on line.OrderId equals order.Id
            where order.Status == SalesOrderStatus.Confirmed && line.DraftNumber == order.DraftNumber &&
                (line.Category == ProductCategory.Lens || line.Category == ProductCategory.ContactLens) && (customerId == null || order.CustomerId == customerId)
            orderby order.CreatedAtUtc, order.Id, line.Position
            select new TechnicalOrderLine(order.Id, order.Version, order.CustomerId, line.Id, line.VariantId, line.ProductName,
                line.VariantCode, line.Category, line.Quantity, line.Source)).Skip(skip).Take(51).ToListAsync(ct);
        return new(rows.Take(50).ToArray(), page, rows.Count > 50);
    }
    public async Task<OrderTechnicalProjection?> ProjectionAsync(Guid orderId, Guid lineId, CancellationToken ct = default)
    {
        if (!current.HasPermission(Permissions.ReadSales) && !current.HasPermission(Permissions.ReadClinical)) throw new UnauthorizedAccessException();
        var actor = current.Id is { Length: > 0 } id ? id : throw new UnauthorizedAccessException();
        await using var db = await factory.CreateDbContextAsync(ct);
        var assignment = await db.OrderTechnicalAssignments.AsNoTracking().SingleOrDefaultAsync(x => x.LineId == lineId && x.IsCurrent &&
            db.SalesLines.Any(l => l.Id == x.LineId && l.OrderId == orderId && db.SalesOrders.Any(o => o.Id == orderId && o.DraftNumber == l.DraftNumber)), ct);
        if (assignment is null) return null;
        var source = await OrderTechnicalRules.LoadAsync(db, assignment.PublicationId, ct);
        if (source is null) return null;
        db.AuditEntries.Add(new() { ActorId = actor, Action = "sales.technical.read", TargetId = lineId.ToString(), OccurredAt = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct);
        return TechnicalProjectionFactory.Create(orderId, lineId, assignment, source);
    }
}
