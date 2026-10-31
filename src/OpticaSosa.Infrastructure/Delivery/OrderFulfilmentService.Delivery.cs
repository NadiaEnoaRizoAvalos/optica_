using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Commerce;
using OpticaSosa.Application.Delivery;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Commerce;
using OpticaSosa.Domain.Delivery;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Domain.Work;
using OpticaSosa.Infrastructure.Commerce;
using OpticaSosa.Infrastructure.Inventory;

namespace OpticaSosa.Infrastructure.Delivery;

public sealed partial class OrderFulfilmentService
{
    public Task<SalesResult> DeliverAsync(Guid orderId, DeliverOrderInput input, CancellationToken ct = default)
    {
        var actor = SalesOperations.Demand(current, Permissions.DeliverOrders);
        return SalesOperations.ExecuteAsync(factory, clock, actor, orderId, input.SubmissionId, "delivery.order.completed", input, async db =>
        {
            var order = await db.SalesOrders.SingleOrDefaultAsync(x => x.Id == orderId, ct);
            if (order is null) return SalesOperations.Missing();
            if (order.Version != input.OrderVersion) return SalesOperations.Conflict();
            if (order.Status != SalesOrderStatus.Confirmed || order.BalanceMinor != 0) return SalesOperations.Invalid("La entrega exige pedido confirmado y saldo cero.");
            var lines = await db.SalesLines.Where(x => x.OrderId == orderId && x.DraftNumber == order.DraftNumber).ToListAsync(ct);
            var bindings = await db.Set<CommercialWorkLine>().Where(x => x.IsCurrent && db.Set<CommercialWork>().Any(g => g.WorkId == x.WorkId && g.OrderId == orderId && g.IsCurrent)).ToListAsync(ct);
            if (lines.Count == 0 || bindings.Count != lines.Count || !lines.Select(x => x.Id).ToHashSet().SetEquals(bindings.Select(x => x.LineId)))
                return SalesOperations.Invalid("La entrega es completa: asociá todas las líneas a sus trabajos.");
            var groups = await db.Set<CommercialWork>().Where(x => x.OrderId == orderId && x.IsCurrent).ToListAsync(ct);
            if (input.Works is null || input.Works.Any(x => x is null) || input.Works.Count != groups.Count || input.Works.Select(x => x.WorkId).Distinct().Count() != groups.Count ||
                !groups.Select(x => x.WorkId).ToHashSet().SetEquals(input.Works.Select(x => x.WorkId)))
                return SalesOperations.Invalid("Confirmá todos los trabajos actuales; no se admiten entregas parciales.");
            var now = clock.GetUtcNow().UtcDateTime; var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            // Primero validar todos los conjuntos; no consumir el primero si otro no puede entregarse.
            var works = new Dictionary<Guid, OpticalWork>();
            foreach (var group in groups)
            {
                var work = await db.OpticalWorks.SingleAsync(x => x.Id == group.WorkId, ct);
                if (work.Version != input.Works.Single(x => x.WorkId == work.Id).Version) return SalesOperations.Conflict();
                var readiness = await CommercialWorkRules.CheckAsync(db, group, clock, true, ct);
                if (!readiness.Ready) return SalesOperations.Invalid(readiness.Error!);
                if (work.Status != WorkStatus.ReadyForDelivery || !work.TechnicalApproved || !work.DeliveryApproved ||
                    group.TechnicalMaterialHash != readiness.MaterialHash || group.DeliveryMaterialHash != readiness.MaterialHash)
                    return SalesOperations.Invalid("Completá ambos controles sobre los materiales actuales; si cambiaron, iniciá una nueva revisión.");
                works.Add(work.Id, work);
            }
            foreach (var work in works.Values)
            {
                var from = work.Status; work.Transition("deliver");
                db.WorkTransitions.Add(new(work, from, actor, now, "delivery.order.completed"));
            }
            foreach (var binding in bindings)
            {
                foreach (var reservation in await db.StockReservations.Where(x => x.LineId == binding.LineId && x.Status == ReservationStatus.Active).ToListAsync(ct))
                {
                    var batch = await db.StockBatches.SingleAsync(x => x.Id == reservation.BatchId, ct);
                    batch.Consume(reservation.Quantity, today); reservation.Close(ReservationStatus.Consumed, actor, now, "Entrega comercial completa");
                    db.StockMovements.Add(new(Guid.NewGuid(), batch.Id, reservation.Id, StockMovementKind.Consume, -reservation.Quantity,
                        -reservation.Quantity, 0, "Entrega comercial completa", StockService.Hash(new { input.SubmissionId, reservation.Id }), actor, now));
                    db.StockDeliveryProofs.Add(new(reservation, works[binding.WorkId]));
                }
            }
            db.Set<OrderDelivery>().Add(new(input.SubmissionId, orderId, actor, now)); order.RegisterOperationalChange();
            return null;
        }, ct);
    }
}
