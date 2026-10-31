using Microsoft.EntityFrameworkCore;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Commerce;
using OpticaSosa.Domain.Delivery;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Domain.Technical;
using OpticaSosa.Infrastructure.Inventory;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Delivery;

internal sealed record WorkReadiness(string? Error, string MaterialHash = "")
{
    internal bool Ready => Error is null;
}

internal static class CommercialWorkRules
{
    internal static async Task<string?> TechnicalAsync(OpticaDbContext db, SalesOrder order, IReadOnlyList<CommercialWorkLine> bindings,
        CancellationToken ct)
    {
        Guid? revision = null;
        var frameIds = await db.SalesLines.Where(l => bindings.Select(b => b.LineId).Contains(l.Id) && l.Category == ProductCategory.Frame)
            .Select(l => l.VariantId).ToListAsync(ct);
        foreach (var binding in bindings)
        {
            var line = await db.SalesLines.AsNoTracking().SingleOrDefaultAsync(x => x.Id == binding.LineId && x.OrderId == order.Id && x.DraftNumber == order.DraftNumber, ct);
            if (line is null) return "Un material no pertenece al pedido confirmado.";
            var variant = await db.ProductVariants.AsNoTracking().SingleAsync(x => x.Id == line.VariantId, ct);
            if (!variant.IsActive || !await db.Products.AnyAsync(x => x.Id == variant.ProductId && x.IsActive, ct)) return "Hay un producto inactivo.";
            if (!OrderTechnicalRules.Required(line.Category))
            {
                if (binding.TechnicalAssignmentId is not null) return "Un material no clínico no debe recibir una asignación clínica.";
                continue;
            }
            var assignment = await db.OrderTechnicalAssignments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == binding.TechnicalAssignmentId && x.LineId == line.Id && x.IsCurrent, ct);
            var source = assignment is null ? null : await OrderTechnicalRules.LoadAsync(db, assignment.PublicationId, ct);
            if (source is null || source.CustomerId != order.CustomerId || source.Status != PublicationStatus.Current)
                return "Falta autorización técnica vigente del Profesional para los materiales del trabajo.";
            var error = OrderTechnicalRules.Validate(line, variant, source.Publication, assignment!.Eyes);
            if (error is not null) return error;
            if (revision is not null && revision != source.Publication.SourceRevisionId) return "Los materiales del conjunto deben corresponder a la misma versión de receta.";
            revision = source.Publication.SourceRevisionId;
            if (source.Publication.Request.Mounting.FrameVariantId is { } frame && frameIds.Count > 0 && (frameIds.Count != 1 || frameIds[0] != frame))
                return "El armazón del conjunto no coincide con el montaje autorizado.";
        }
        return null;
    }

    internal static async Task<WorkReadiness> CheckAsync(OpticaDbContext db, CommercialWork group, TimeProvider clock, bool requireBalance, CancellationToken ct)
    {
        if (!group.IsCurrent) return new("El trabajo fue reemplazado; consultá su versión vigente.");
        var order = await db.SalesOrders.AsNoTracking().SingleAsync(x => x.Id == group.OrderId, ct);
        if (order.Status != SalesOrderStatus.Confirmed || !await db.Customers.AnyAsync(x => x.Id == order.CustomerId && x.IsActive, ct))
            return new("El pedido debe estar confirmado y su cliente activo.");
        if (await db.Set<OrderDelivery>().AnyAsync(x => x.OrderId == order.Id, ct)) return new("El pedido ya fue entregado.");
        if (requireBalance && order.BalanceMinor != 0) return new("El pedido tiene saldo pendiente.");
        var bindings = await db.Set<CommercialWorkLine>().AsNoTracking().Where(x => x.WorkId == group.WorkId && x.IsCurrent).OrderBy(x => x.LineId).ToListAsync(ct);
        if (bindings.Count == 0) return new("El trabajo no tiene materiales asociados.");
        var error = await TechnicalAsync(db, order, bindings, ct);
        if (error is not null) return new(error);
        var fingerprints = new List<object>(); var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        foreach (var binding in bindings)
        {
            var line = await db.SalesLines.AsNoTracking().SingleAsync(x => x.Id == binding.LineId, ct);
            var reservations = await db.StockReservations.AsNoTracking().Where(x => x.LineId == line.Id && x.Status == ReservationStatus.Active).OrderBy(x => x.Id).ToListAsync(ct);
            if (reservations.Sum(x => (long)x.Quantity) != line.Quantity) return new("Faltan materiales recibidos y reservados para completar el trabajo.");
            foreach (var reservation in reservations)
            {
                var batch = await db.StockBatches.AsNoTracking().SingleAsync(x => x.Id == reservation.BatchId, ct);
                if (batch.VariantId != line.VariantId || reservation.TechnicalAssignmentId != binding.TechnicalAssignmentId ||
                    (batch.ExpiresOn is { } expiry && today > expiry) || batch.OnHand - batch.Quarantined < batch.Reserved)
                    return new("Un material reservado está vencido, observado o no coincide con la autorización.");
                var lab = await (from receipt in db.LaboratoryReceipts join encargo in db.LaboratoryOrders on receipt.LaboratoryOrderId equals encargo.Id
                    where receipt.BatchId == batch.Id select encargo).AsNoTracking().SingleOrDefaultAsync(ct);
                if ((line.Source == FulfilmentSource.Stock && lab is not null) || (line.Source == FulfilmentSource.Laboratory &&
                    (lab is null || lab.LineId != line.Id || lab.TechnicalAssignmentId != binding.TechnicalAssignmentId)))
                    return new("El material de laboratorio no corresponde al trabajo y su autorización.");
                fingerprints.Add(new { binding.LineId, binding.TechnicalAssignmentId, reservation.Id, reservation.Version, reservation.Quantity, BatchId = batch.Id });
            }
        }
        return new(null, StockService.Hash(fingerprints));
    }

    internal static async Task<(CommercialWork? Group, WorkReadiness Readiness)> ForWorkAsync(OpticaDbContext db, Guid id,
        TimeProvider clock, bool requireBalance, CancellationToken ct)
    {
        var group = await db.Set<CommercialWork>().SingleOrDefaultAsync(x => x.WorkId == id, ct);
        return (group, group is null ? new(null) : await CheckAsync(db, group, clock, requireBalance, ct));
    }
}
