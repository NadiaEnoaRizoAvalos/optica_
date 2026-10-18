using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Inventory;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Inventory;

// Compartir construcción/validación del mismo libro; el caso de uso mantiene su transacción e idempotencia.
internal static class InventoryReceiptWriter
{
    internal static async Task<StockBatch> AddAsync(OpticaDbContext db, ProductVariant variant, StockReceiptInput input,
        string actor, TimeProvider clock, CancellationToken ct)
    {
        var batch = new StockBatch(input.SubmissionId, variant, input.Quantity, input.Quarantined, input.Lot,
            input.Expiry, input.ExpiryPrecision, actor, clock.GetUtcNow().UtcDateTime);
        if (input.QuarantinedContactPackageOpened is not null && (batch.Category != ProductCategory.ContactLens || input.Quarantined == 0))
            throw new ArgumentException("El estado del envase corresponde a contactos ingresados en cuarentena.");
        if (batch.LotKey is not null && await db.StockBatches.AnyAsync(x => x.VariantId == batch.VariantId && x.LotKey == batch.LotKey &&
            (x.ExpiresOn != batch.ExpiresOn || x.ExpiryPrecision != batch.ExpiryPrecision), ct))
            throw new ArgumentException("El lote ya registrado tiene otro vencimiento o precisión; verificá el envase.");
        var movement = new StockMovement(input.SubmissionId, batch.Id, null, StockMovementKind.Receipt,
            input.Quantity, 0, input.Quarantined, input.Reason, StockService.Hash(input), actor, clock.GetUtcNow().UtcDateTime);
        variant.LockSpecification(); db.StockBatches.Add(batch); db.StockMovements.Add(movement);
        if (input.Quarantined > 0) db.StockHolds.Add(new(input.SubmissionId, batch.Id, StockHoldOrigin.Receipt, input.Quarantined,
            input.QuarantinedContactPackageOpened == true, input.Reason, actor, clock.GetUtcNow().UtcDateTime));
        db.AuditEntries.Add(new() { ActorId = actor, Action = "inventory.received", TargetId = batch.Id.ToString(), OccurredAt = clock.GetUtcNow() });
        return batch;
    }
}
