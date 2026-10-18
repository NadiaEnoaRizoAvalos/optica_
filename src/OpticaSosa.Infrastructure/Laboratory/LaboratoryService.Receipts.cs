using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Commerce;
using OpticaSosa.Application.Inventory;
using OpticaSosa.Application.Laboratory;
using OpticaSosa.Infrastructure.Commerce;
using OpticaSosa.Infrastructure.Inventory;

namespace OpticaSosa.Infrastructure.Laboratory;

public sealed partial class LaboratoryService
{
    public Task<SalesResult> ReceiveAsync(Guid orderId, Guid laboratoryOrderId, LaboratoryReceiveInput input, CancellationToken ct = default) =>
        MutateAsync(orderId, laboratoryOrderId, input.SubmissionId, input.OrderVersion, input.LaboratoryVersion, "laboratory.received", input, async (db, context, actor) =>
        {
            var technical = await TechnicalAsync(db, context, ct);
            if (!technical.Valid && input.Quantity != input.Quarantined)
                return SalesOperations.Invalid("La técnica cambió o se revocó: registrá la llegada exclusivamente en cuarentena y solicitá una nueva autorización/encargo.");
            context.Laboratory.Receive(input.Quantity, input.Quarantined, clock.GetUtcNow().UtcDateTime);
            var variant = await db.ProductVariants.SingleAsync(x => x.Id == context.Line.VariantId, ct);
            var receipt = new StockReceiptInput(input.SubmissionId, variant.Id, variant.Version, input.Quantity, input.Quarantined,
                input.Lot, input.Expiry, input.ExpiryPrecision, input.Reason, input.QuarantinedContactPackageOpened);
            var batch = await InventoryReceiptWriter.AddAsync(db, variant, receipt, actor, clock, ct);
            db.LaboratoryReceipts.Add(new(input.SubmissionId, laboratoryOrderId, batch.Id, input.Quantity, input.Quarantined,
                input.Reference, input.Reason, actor, clock.GetUtcNow().UtcDateTime));
            return null;
        }, ct);
}
