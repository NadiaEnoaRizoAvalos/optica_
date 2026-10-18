using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Laboratory;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Commerce;
using OpticaSosa.Infrastructure.Commerce;

namespace OpticaSosa.Infrastructure.Laboratory;

public sealed partial class LaboratoryService
{
    public async Task<LaboratoryReceiptPage?> ReceiptsAsync(Guid orderId, Guid laboratoryOrderId, int page = 1, CancellationToken ct = default)
    {
        SalesOperations.Demand(current, Permissions.ReadLaboratory);
        var skip = SalesOperations.Skip(page);
        await using var db = await factory.CreateDbContextAsync(ct);
        if (await LoadAsync(db, orderId, laboratoryOrderId, ct) is null) return null;
        var rows = await db.LaboratoryReceipts.AsNoTracking().Where(x => x.LaboratoryOrderId == laboratoryOrderId)
            .OrderByDescending(x => x.ReceivedAtUtc).ThenByDescending(x => x.Id).Skip(skip).Take(51).ToListAsync(ct);
        return new(rows.Take(50).Select(x => new LaboratoryReceiptView(x.Id, x.BatchId, x.Quantity, x.QuarantinedQuantity,
            x.Reference, x.Reason, x.ActorUserId, x.ReceivedAtUtc)).ToArray(), page, rows.Count > 50);
    }
    public async Task<LaboratoryChangePage?> ChangesAsync(Guid orderId, Guid laboratoryOrderId, int page = 1, CancellationToken ct = default)
    {
        SalesOperations.Demand(current, Permissions.ReadLaboratory);
        var skip = SalesOperations.Skip(page);
        await using var db = await factory.CreateDbContextAsync(ct);
        if (await LoadAsync(db, orderId, laboratoryOrderId, ct) is null) return null;
        var rows = await db.LaboratoryChanges.AsNoTracking().Where(x => x.LaboratoryOrderId == laboratoryOrderId)
            .OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.Id).Skip(skip).Take(51).ToListAsync(ct);
        return new(rows.Take(50).Select(x => new LaboratoryChangeView(x.Id, x.Kind, x.CancelledQuantity, x.PreviousEstimatedDate,
            x.NewEstimatedDate, x.Reason, x.LaboratoryConfirmation, x.ActorUserId, x.OccurredAtUtc)).ToArray(), page, rows.Count > 50);
    }
    public async Task<LaboratoryDocumentResult> DocumentAsync(Guid orderId, Guid laboratoryOrderId, CancellationToken ct = default)
    {
        var actor = SalesOperations.Demand(current, Permissions.ReadLaboratory);
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var context = await LoadAsync(db, orderId, laboratoryOrderId, ct);
        if (context is null) return new(null, "No se encontró el encargo del pedido.");
        var technical = await TechnicalAsync(db, context, ct);
        if (context.Order.Status != SalesOrderStatus.Confirmed || !technical.Valid || context.Laboratory.PendingQuantity <= 0)
            return new(null, "No se genera documentación para fabricar con autorización obsoleta, cliente inactivo o encargo cerrado.");
        var lab = context.Laboratory;
        db.AuditEntries.Add(new() { ActorId = actor, Action = "laboratory.document.generated", TargetId = lab.Id.ToString(), OccurredAt = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(new(lab.Id, context.SupplierName, orderId, context.Line.Id, context.Line.ProductName, context.Line.VariantCode,
            context.Line.Unit, context.Line.UnitsPerPackage, lab.Quantity, lab.CancelledQuantity, lab.EstimatedDate, technical.Projection, clock.GetUtcNow().UtcDateTime));
    }
}
