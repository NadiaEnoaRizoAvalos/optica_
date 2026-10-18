using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Commerce;
using OpticaSosa.Application.Laboratory;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Commerce;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Domain.Laboratory;
using OpticaSosa.Domain.Technical;
using OpticaSosa.Infrastructure.Commerce;
using OpticaSosa.Infrastructure.Inventory;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Laboratory;

public sealed partial class LaboratoryService
{
    public Task<SalesResult> CreateAsync(Guid orderId, Guid lineId, LaboratoryCreateInput input, CancellationToken ct = default)
    {
        var actor = SalesOperations.Demand(current, Permissions.WriteLaboratory);
        return SalesOperations.ExecuteAsync(factory, clock, actor, orderId, input.SubmissionId, "laboratory.authorized", new { lineId, input }, async db =>
        {
            if (!input.CommercialConfirmed) return SalesOperations.Invalid("Confirmá explícitamente la autorización comercial del encargo.");
            var order = await db.SalesOrders.SingleOrDefaultAsync(x => x.Id == orderId, ct);
            if (order is null) return SalesOperations.Missing();
            if (order.Version != input.OrderVersion) return SalesOperations.Conflict();
            if (order.Status != SalesOrderStatus.Confirmed) return SalesOperations.Invalid("El pedido comercial debe estar confirmado.");
            var line = await db.SalesLines.SingleOrDefaultAsync(x => x.Id == lineId && x.OrderId == orderId && x.DraftNumber == order.DraftNumber, ct);
            if (line is null) return SalesOperations.Missing();
            if (line.Source != FulfilmentSource.Laboratory) return SalesOperations.Invalid("La línea no es un encargo de laboratorio; no sustituir su modalidad automáticamente.");
            var supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == input.SupplierId && x.IsActive && x.IsLaboratory, ct);
            if (supplier is null) return SalesOperations.Invalid("Seleccioná un laboratorio activo.");
            if (supplier.Version != input.SupplierVersion) return SalesOperations.Conflict();
            var variant = await db.ProductVariants.SingleAsync(x => x.Id == line.VariantId, ct);
            if (!variant.IsActive || !await db.Products.AnyAsync(x => x.Id == variant.ProductId && x.IsActive, ct)) return SalesOperations.Invalid("El producto está inactivo.");
            var lab = new LaboratoryOrder(input.SubmissionId, lineId, supplier.Id, input.TechnicalAssignmentId, input.Quantity, input.EstimatedDate, actor, clock.GetUtcNow().UtcDateTime);
            var technical = await TechnicalAsync(db, new() { Laboratory = lab, Order = order, Line = line, SupplierName = supplier.Name }, ct);
            if (!technical.Valid) return SalesOperations.Invalid("Falta una asignación técnica vigente del Profesional para este cliente y producto.");
            var covered = await CoveredAsync(db, lineId, input.TechnicalAssignmentId, DateOnly.FromDateTime(clock.GetLocalNow().DateTime), ct);
            if (input.Quantity > line.Quantity - covered) return SalesOperations.Invalid("El encargo supera la cantidad del pedido todavía sin cobertura.");
            db.LaboratoryOrders.Add(lab); order.RegisterOperationalChange();
            return null;
        }, ct);
    }
    private static async Task<long> CoveredAsync(OpticaDbContext db, Guid lineId, Guid? assignmentId, DateOnly today, CancellationToken ct)
    {
        var labs = db.LaboratoryOrders.Where(x => x.LineId == lineId && x.TechnicalAssignmentId == assignmentId);
        var pending = await labs.SumAsync(x => (long)x.Quantity - x.ReceivedQuantity - x.CancelledQuantity, ct);
        var batches = from receipt in db.LaboratoryReceipts join lab in labs on receipt.LaboratoryOrderId equals lab.Id
                      join batch in db.StockBatches on receipt.BatchId equals batch.Id select batch;
        var receivedUsable = await batches.Where(x => x.ExpiresOn == null || x.ExpiresOn >= today).SumAsync(x => (long)x.OnHand - x.Quarantined, ct);
        var consumed = await db.StockReservations.Where(x => x.LineId == lineId && x.Status == ReservationStatus.Consumed &&
            batches.Any(b => b.Id == x.BatchId)).SumAsync(x => (long)x.Quantity, ct);
        return checked(pending + receivedUsable + consumed);
    }
    private Task<SalesResult> MutateAsync(Guid orderId, Guid labId, Guid submission, Guid orderVersion, Guid labVersion,
        string action, object request, Func<OpticaDbContext, Context, string, Task<SalesResult?>> mutate, CancellationToken ct)
    {
        var actor = SalesOperations.Demand(current, Permissions.WriteLaboratory);
        return SalesOperations.ExecuteAsync(factory, clock, actor, orderId, submission, action, new { labId, request }, async db =>
        {
            var context = await LoadAsync(db, orderId, labId, ct);
            if (context is null) return SalesOperations.Missing();
            if (context.Order.Version != orderVersion || context.Laboratory.Version != labVersion) return SalesOperations.Conflict();
            if (context.Order.Status != SalesOrderStatus.Confirmed) return SalesOperations.Invalid("El pedido no está confirmado o ya fue cancelado.");
            var result = await mutate(db, context, actor);
            if (result is null) context.Order.RegisterOperationalChange();
            return result;
        }, ct);
    }
    public Task<SalesResult> RecordDispatchAsync(Guid orderId, Guid laboratoryOrderId, LaboratoryDispatchInput input, CancellationToken ct = default) =>
        MutateAsync(orderId, laboratoryOrderId, input.SubmissionId, input.OrderVersion, input.LaboratoryVersion, "laboratory.dispatch.recorded", input, async (db, context, actor) =>
        {
            if (!await db.Suppliers.AnyAsync(x => x.Id == context.Laboratory.SupplierId && x.IsActive && x.IsLaboratory, ct)) return SalesOperations.Invalid("El laboratorio está inactivo.");
            if (!(await TechnicalAsync(db, context, ct)).Valid) return SalesOperations.Invalid("La técnica ya no está vigente; no se autoriza fabricación.");
            context.Laboratory.RecordDispatch(actor, clock.GetUtcNow().UtcDateTime, input.Reference); return null;
        }, ct);
    public Task<SalesResult> CancelAsync(Guid orderId, Guid laboratoryOrderId, LaboratoryCancelInput input, CancellationToken ct = default) =>
        MutateAsync(orderId, laboratoryOrderId, input.SubmissionId, input.OrderVersion, input.LaboratoryVersion, "laboratory.cancelled", input, (db, context, actor) =>
        {
            context.Laboratory.Cancel(input.Quantity, input.LaboratoryConfirmation);
            db.LaboratoryChanges.Add(new(input.SubmissionId, laboratoryOrderId, LaboratoryChangeKind.Cancel, input.Quantity, null, null,
                input.Reason, input.LaboratoryConfirmation, actor, clock.GetUtcNow().UtcDateTime));
            return Task.FromResult<SalesResult?>(null);
        }, ct);
    public Task<SalesResult> RescheduleAsync(Guid orderId, Guid laboratoryOrderId, LaboratoryRescheduleInput input, CancellationToken ct = default) =>
        MutateAsync(orderId, laboratoryOrderId, input.SubmissionId, input.OrderVersion, input.LaboratoryVersion, "laboratory.rescheduled", input, (db, context, actor) =>
        {
            var previous = context.Laboratory.EstimatedDate; context.Laboratory.Reschedule(input.EstimatedDate);
            db.LaboratoryChanges.Add(new(input.SubmissionId, laboratoryOrderId, LaboratoryChangeKind.Reschedule, 0, previous, input.EstimatedDate,
                input.Reason, null, actor, clock.GetUtcNow().UtcDateTime));
            return Task.FromResult<SalesResult?>(null);
        }, ct);
}
