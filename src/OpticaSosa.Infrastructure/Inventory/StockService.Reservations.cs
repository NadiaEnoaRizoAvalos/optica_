using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Commerce;
using OpticaSosa.Application.Inventory;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Commerce;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Domain.Technical;
using OpticaSosa.Infrastructure.Commerce;

namespace OpticaSosa.Infrastructure.Inventory;

public sealed partial class StockService
{
    public Task<SalesResult> ReserveAsync(Guid orderId, Guid lineId, ReserveStockInput input, CancellationToken ct = default)
    {
        var actor = SalesOperations.Demand(current, Permissions.WriteSales);
        return SalesOperations.ExecuteAsync(factory, clock, actor, orderId, input.SubmissionId, "inventory.reserved", new { lineId, input }, async db =>
        {
            var order = await db.SalesOrders.SingleOrDefaultAsync(x => x.Id == orderId, ct);
            if (order is null) return SalesOperations.Missing();
            if (order.Version != input.OrderVersion) return SalesOperations.Conflict();
            if (order.Status != SalesOrderStatus.Confirmed || !await db.Customers.AnyAsync(x => x.Id == order.CustomerId && x.IsActive, ct))
                return SalesOperations.Invalid("La reserva requiere un pedido confirmado de cliente activo.");
            var line = await db.SalesLines.SingleOrDefaultAsync(x => x.Id == lineId && x.OrderId == orderId && x.DraftNumber == order.DraftNumber, ct);
            if (line is null) return SalesOperations.Missing();
            if (line.Source != FulfilmentSource.Stock) return SalesOperations.Invalid("El material encargado sólo se reserva mediante su recepción de laboratorio validada.");
            var batch = await db.StockBatches.SingleOrDefaultAsync(x => x.Id == input.BatchId && x.VariantId == line.VariantId, ct);
            if (batch is null) return SalesOperations.Invalid("Seleccioná existencias recibidas de la variante exacta del pedido.");
            if (batch.Version != input.BatchVersion) return SalesOperations.Conflict();
            var variant = await db.ProductVariants.SingleAsync(x => x.Id == line.VariantId, ct);
            if (!variant.IsActive || !await db.Products.AnyAsync(x => x.Id == variant.ProductId && x.IsActive, ct)) return SalesOperations.Invalid("El producto está inactivo.");
            var assigned = await db.OrderTechnicalAssignments.AsNoTracking().SingleOrDefaultAsync(x => x.LineId == lineId && x.IsCurrent, ct);
            if (OrderTechnicalRules.Required(line.Category))
            {
                if (assigned is null || assigned.Id != input.TechnicalAssignmentId) return SalesOperations.Invalid("Falta la asignación técnica vigente del Profesional.");
                var source = await OrderTechnicalRules.LoadAsync(db, assigned.PublicationId, ct);
                if (source is null || source.CustomerId != order.CustomerId || source.Status != PublicationStatus.Current)
                    return SalesOperations.Invalid("La autorización técnica ya no está vigente; solicitá su revisión al Profesional.");
                var error = OrderTechnicalRules.Validate(line, variant, source.Publication, assigned.Eyes);
                if (error is not null) return SalesOperations.Invalid(error);
            }
            else if (input.TechnicalAssignmentId is not null) return SalesOperations.Invalid("El producto no necesita una autorización de graduaciones.");
            var already = await db.StockReservations.Where(x => x.LineId == lineId && x.Status != ReservationStatus.Released).SumAsync(x => (long)x.Quantity, ct);
            if (input.Quantity <= 0 || input.Quantity > line.Quantity - already) return SalesOperations.Invalid("La reserva supera la cantidad pendiente del pedido.");
            batch.Reserve(input.Quantity, Today);
            var reservation = new StockReservation(input.SubmissionId, lineId, batch.Id, assigned?.Id, input.Quantity, actor, clock.GetUtcNow().UtcDateTime);
            db.StockReservations.Add(reservation);
            db.StockMovements.Add(new(input.SubmissionId, batch.Id, reservation.Id, StockMovementKind.Reserve,
                0, input.Quantity, 0, "Reserva explícita del pedido", Hash(new { lineId, input }), actor, clock.GetUtcNow().UtcDateTime));
            order.RegisterOperationalChange();
            return null;
        }, ct);
    }
    public Task<SalesResult> ReleaseAsync(Guid orderId, Guid reservationId, ReleaseStockInput input, CancellationToken ct = default)
    {
        var actor = SalesOperations.Demand(current, Permissions.WriteSales);
        return SalesOperations.ExecuteAsync(factory, clock, actor, orderId, input.SubmissionId, "inventory.released", new { reservationId, input }, async db =>
        {
            var order = await db.SalesOrders.SingleOrDefaultAsync(x => x.Id == orderId, ct);
            if (order is null) return SalesOperations.Missing();
            if (order.Version != input.OrderVersion) return SalesOperations.Conflict();
            var reservation = await db.StockReservations.SingleOrDefaultAsync(x => x.Id == reservationId &&
                db.SalesLines.Any(l => l.Id == x.LineId && l.OrderId == orderId), ct);
            if (reservation is null) return SalesOperations.Missing();
            if (reservation.Version != input.ReservationVersion) return SalesOperations.Conflict();
            reservation.Close(ReservationStatus.Released, actor, clock.GetUtcNow().UtcDateTime, input.Reason);
            var batch = await db.StockBatches.SingleAsync(x => x.Id == reservation.BatchId, ct);
            batch.Release(reservation.Quantity);
            db.StockMovements.Add(new(input.SubmissionId, batch.Id, reservation.Id, StockMovementKind.Release,
                0, -reservation.Quantity, 0, input.Reason, Hash(new { reservationId, input }), actor, clock.GetUtcNow().UtcDateTime));
            order.RegisterOperationalChange();
            return null;
        }, ct);
    }
}
