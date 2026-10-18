using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Inventory;
using OpticaSosa.Application.Laboratory;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Commerce;
using OpticaSosa.Domain.Laboratory;
using OpticaSosa.Domain.Technical;
using OpticaSosa.Infrastructure.Commerce;
using OpticaSosa.Infrastructure.Inventory;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Laboratory;

public sealed partial class LaboratoryService(IDbContextFactory<OpticaDbContext> factory, ICurrentUser current, TimeProvider clock) : ILaboratoryService
{
    private sealed record Context
    {
        public required LaboratoryOrder Laboratory { get; init; }
        public required SalesOrder Order { get; init; }
        public required SalesLine Line { get; init; }
        public required string SupplierName { get; init; }
    }
    private static IQueryable<Context> Query(OpticaDbContext db) =>
        from lab in db.LaboratoryOrders join line in db.SalesLines on lab.LineId equals line.Id
        join order in db.SalesOrders on line.OrderId equals order.Id join supplier in db.Suppliers on lab.SupplierId equals supplier.Id
        select new Context { Laboratory = lab, Order = order, Line = line, SupplierName = supplier.Name };
    private static Task<Context?> LoadAsync(OpticaDbContext db, Guid orderId, Guid labId, CancellationToken ct) =>
        Query(db).SingleOrDefaultAsync(x => x.Order.Id == orderId && x.Laboratory.Id == labId, ct);
    private async Task<(bool Valid, OrderTechnicalProjection? Projection)> TechnicalAsync(OpticaDbContext db, Context context, CancellationToken ct)
    {
        if (!await db.Customers.AnyAsync(x => x.Id == context.Order.CustomerId && x.IsActive, ct)) return (false, null);
        var variant = await db.ProductVariants.AsNoTracking().SingleAsync(x => x.Id == context.Line.VariantId, ct);
        if (!variant.IsActive || !await db.Products.AnyAsync(x => x.Id == variant.ProductId && x.IsActive, ct)) return (false, null);
        if (!OrderTechnicalRules.Required(context.Line.Category)) return (context.Laboratory.TechnicalAssignmentId is null, null);
        var assignment = await db.OrderTechnicalAssignments.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == context.Laboratory.TechnicalAssignmentId && x.LineId == context.Line.Id && x.IsCurrent, ct);
        if (assignment is null) return (false, null);
        var source = await OrderTechnicalRules.LoadAsync(db, assignment.PublicationId, ct);
        if (source is null || source.CustomerId != context.Order.CustomerId || source.Status != PublicationStatus.Current) return (false, null);
        if (OrderTechnicalRules.Validate(context.Line, variant, source.Publication, assignment.Eyes) is not null) return (false, null);
        return (true, TechnicalProjectionFactory.Create(context.Order.Id, context.Line.Id, assignment, source));
    }
    private async Task<LaboratoryDetails> DetailsAsync(OpticaDbContext db, Context x, CancellationToken ct)
    {
        var l = x.Laboratory; var state = await TechnicalAsync(db, x, ct);
        return new(l.Id, x.Order.Id, x.Order.Version, x.Order.CustomerId, l.LineId, l.SupplierId, x.SupplierName, l.TechnicalAssignmentId,
            l.Quantity, l.ReceivedQuantity, l.QuarantinedQuantity, l.CancelledQuantity, l.PendingQuantity, l.Status, state.Valid,
            l.EstimatedDate, l.PendingQuantity > 0 && l.EstimatedDate is { } date && DateOnly.FromDateTime(clock.GetLocalNow().DateTime) > date,
            l.AuthorizedByUserId, l.AuthorizedAtUtc, l.SentAtUtc, l.DispatchReference, l.LastReceivedAtUtc, l.Version);
    }
    public async Task<LaboratoryPage> ListAsync(Guid? orderId = null, int page = 1, CancellationToken ct = default)
    {
        SalesOperations.Demand(current, Permissions.ReadLaboratory);
        var skip = SalesOperations.Skip(page);
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await Query(db).AsNoTracking().Where(x => orderId == null || x.Order.Id == orderId)
            .OrderByDescending(x => x.Laboratory.AuthorizedAtUtc).ThenByDescending(x => x.Laboratory.Id).Skip(skip).Take(51).ToListAsync(ct);
        var views = new List<LaboratoryDetails>();
        foreach (var row in rows.Take(50)) views.Add(await DetailsAsync(db, row, ct));
        return new(views, page, rows.Count > 50);
    }
    public async Task<LaboratoryDetails?> GetAsync(Guid orderId, Guid laboratoryOrderId, CancellationToken ct = default)
    {
        SalesOperations.Demand(current, Permissions.ReadLaboratory);
        await using var db = await factory.CreateDbContextAsync(ct);
        var context = await Query(db).AsNoTracking().SingleOrDefaultAsync(x => x.Order.Id == orderId && x.Laboratory.Id == laboratoryOrderId, ct);
        return context is null ? null : await DetailsAsync(db, context, ct);
    }
}
