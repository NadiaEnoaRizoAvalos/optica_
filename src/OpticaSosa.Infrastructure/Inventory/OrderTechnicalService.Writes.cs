using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Commerce;
using OpticaSosa.Application.Inventory;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Commerce;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Domain.Technical;
using OpticaSosa.Infrastructure.Commerce;

namespace OpticaSosa.Infrastructure.Inventory;

public sealed partial class OrderTechnicalService
{
    public Task<SalesResult> AssignAsync(Guid orderId, Guid lineId, AssignTechnicalInput input, CancellationToken ct = default)
    {
        var actor = SalesOperations.Demand(current, Permissions.WriteClinical);
        return SalesOperations.ExecuteAsync(factory, clock, actor, orderId, input.SubmissionId, "sales.technical.assigned", new { lineId, input }, async db =>
        {
            var order = await db.SalesOrders.SingleOrDefaultAsync(x => x.Id == orderId, ct);
            if (order is null) return SalesOperations.Missing();
            if (order.Version != input.OrderVersion) return SalesOperations.Conflict();
            if (order.Status != SalesOrderStatus.Confirmed) return SalesOperations.Invalid("El pedido debe estar confirmado.");
            var line = await db.SalesLines.SingleOrDefaultAsync(x => x.Id == lineId && x.OrderId == orderId && x.DraftNumber == order.DraftNumber, ct);
            if (line is null) return SalesOperations.Missing();
            var source = await OrderTechnicalRules.LoadAsync(db, input.PublicationId, ct);
            if (source is null || source.CustomerId != order.CustomerId) return SalesOperations.Invalid("La autorización debe pertenecer al cliente de este pedido.");
            if (source.Publication.Version != input.PublicationVersion) return SalesOperations.Conflict();
            if (source.Status != PublicationStatus.Current) return SalesOperations.Invalid("La autorización ya no está vigente; el Profesional debe revisar y publicar la versión válida.");
            var variant = await db.ProductVariants.SingleAsync(x => x.Id == line.VariantId, ct);
            if (!variant.IsActive || !await db.Products.AnyAsync(x => x.Id == variant.ProductId && x.IsActive, ct)) return SalesOperations.Invalid("El producto está inactivo.");
            var error = OrderTechnicalRules.Validate(line, variant, source.Publication, input.Eyes);
            if (error is not null) return SalesOperations.Invalid(error);
            var previous = await db.OrderTechnicalAssignments.SingleOrDefaultAsync(x => x.LineId == lineId && x.IsCurrent, ct);
            if (previous?.Id != input.PreviousAssignmentId) return SalesOperations.Conflict();
            if (await db.StockReservations.AnyAsync(x => x.LineId == lineId && x.Status != ReservationStatus.Released, ct))
                return SalesOperations.Invalid("Liberá las reservas anteriores; el material consumido requiere su circuito de corrección o devolución.");
            var assignment = new OrderTechnicalAssignment(input.SubmissionId, lineId, input.PublicationId, input.Eyes,
                previous?.Id, input.ReplacementReason, actor, clock.GetUtcNow().UtcDateTime);
            if (previous is not null) { previous.Supersede(); await db.SaveChangesAsync(ct); }
            db.OrderTechnicalAssignments.Add(assignment); order.RegisterOperationalChange();
            return null;
        }, ct);
    }
}
