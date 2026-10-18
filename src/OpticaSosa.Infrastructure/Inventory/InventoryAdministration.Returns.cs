using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Inventory;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Domain.Work;
using OpticaSosa.Infrastructure.Commerce;

namespace OpticaSosa.Infrastructure.Inventory;

public sealed partial class InventoryAdministration
{
    public async Task<InventoryResult> ReturnFromCustomerAsync(Guid reservationId, InventoryReturnInput input, CancellationToken ct = default)
    {
        SalesOperations.Demand(current, Permissions.ReceiveStock);
        Guid? batchId;
        await using (var probe = await factory.CreateDbContextAsync(ct))
            batchId = await probe.StockReservations.Where(x => x.Id == reservationId).Select(x => (Guid?)x.BatchId).SingleOrDefaultAsync(ct);
        if (batchId is null) return Invalid("No se encontró la reserva original.");
        return await ExecuteAsync(batchId.Value, input.SubmissionId, input.BatchVersion, "inventory.returned-by-customer", StockMovementKind.CustomerReturn,
            new { reservationId, input }, async (db, batch, actor, hash) =>
        {
            var reservation = await db.StockReservations.SingleAsync(x => x.Id == reservationId, ct);
            if (reservation.Version != input.ReservationVersion) return Conflict();
            var delivered = await db.StockDeliveryProofs.AnyAsync(x => x.ReservationId == reservationId && db.OpticalWorks.Any(w =>
                w.Id == x.WorkId && w.Status == WorkStatus.Delivered && db.SalesLines.Any(l => l.Id == reservation.LineId &&
                    db.SalesOrders.Any(o => o.Id == l.OrderId && o.CustomerId == w.CustomerId))), ct);
            if (!delivered) return Invalid("Falta evidencia de entrega con calidad aprobada: consumir material no demuestra que el cliente lo recibió.");
            var already = await db.StockCustomerReturns.Where(x => x.ReservationId == reservationId).SumAsync(x => (long)x.Quantity, ct);
            if (input.Quantity <= 0 || input.Quantity > reservation.Quantity - already) return Invalid("La cantidad supera lo entregado todavía no devuelto.");
            var entry = new StockCustomerReturn(input.SubmissionId, input.SubmissionId, reservation, batch.Category, input.Quantity,
                input.ContactPackageOpened, input.Reason, actor, clock.GetUtcNow().UtcDateTime);
            batch.Adjust(input.Quantity, input.Quantity);
            db.StockHolds.Add(new(input.SubmissionId, batch.Id, StockHoldOrigin.CustomerReturn, input.Quantity,
                input.ContactPackageOpened == true, input.Reason, actor, clock.GetUtcNow().UtcDateTime));
            db.StockCustomerReturns.Add(entry);
            Movement(db, input.SubmissionId, batch, reservationId, StockMovementKind.CustomerReturn, input.Quantity, input.Quantity, input.Reason, hash, actor);
            return null;
        }, ct);
    }
}
