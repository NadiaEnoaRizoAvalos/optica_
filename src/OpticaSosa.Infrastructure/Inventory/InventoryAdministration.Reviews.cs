using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Inventory;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Infrastructure.Commerce;

namespace OpticaSosa.Infrastructure.Inventory;

public sealed partial class InventoryAdministration
{
    public async Task<InventoryResult> ReviewAsync(Guid batchId, Guid holdId, InventoryReviewInput input, CancellationToken ct = default)
    {
        SalesOperations.Demand(current, Permissions.ReadStock);
        await using (var probe = await factory.CreateDbContextAsync(ct))
        {
            var category = await probe.StockBatches.Where(x => x.Id == batchId).Select(x => (ProductCategory?)x.Category).SingleOrDefaultAsync(ct);
            if (category is null) return Invalid("No se encontró el lote de inventario.");
            SalesOperations.Demand(current, category is ProductCategory.Lens or ProductCategory.ContactLens ? Permissions.WriteClinical : Permissions.ReviewOperationalStock);
        }
        return await ExecuteAsync(batchId, input.SubmissionId, input.BatchVersion, "inventory.reviewed", null, new { holdId, input }, async (db, batch, actor, hash) =>
        {
            var hold = await HoldAsync(db, batch, holdId, input.HoldVersion, input.Quantity, ct);
            var role = current.HasPermission(Permissions.WriteClinical) ? StockReviewerRole.Professional : StockReviewerRole.Sales;
            var review = new StockReview(input.SubmissionId, hold, batch, input.Quantity, input.Approve, input.ProductMatches,
                input.ItemIntact, input.SealIntact, input.Reason, hash, actor, role, clock.GetUtcNow().UtcDateTime, Today);
            if (input.Approve)
            {
                if (!await db.ProductVariants.AnyAsync(x => x.Id == batch.VariantId && x.IsActive && db.Products.Any(p => p.Id == x.ProductId && p.IsActive), ct))
                    return Invalid("El producto está inactivo; no se libera material para uso.");
                var origin = await (from receipt in db.LaboratoryReceipts join lab in db.LaboratoryOrders on receipt.LaboratoryOrderId equals lab.Id
                    where receipt.BatchId == batch.Id select lab).AsNoTracking().SingleOrDefaultAsync(ct);
                if (origin?.TechnicalAssignmentId is { } technicalId)
                {
                    var assignment = await db.OrderTechnicalAssignments.AsNoTracking().SingleAsync(x => x.Id == technicalId, ct);
                    var source = await OrderTechnicalRules.LoadAsync(db, assignment.PublicationId, ct);
                    if (!assignment.IsCurrent || source?.Status != OpticaSosa.Domain.Technical.PublicationStatus.Current)
                        return Invalid("El material personalizado conserva una autorización obsoleta; no puede convertirse en stock genérico ni liberarse sin el circuito autorizado.");
                }
                hold.Release(input.Quantity); batch.Adjust(0, -input.Quantity);
                Movement(db, input.SubmissionId, batch, null, StockMovementKind.ReviewRelease, 0, -input.Quantity, input.Reason, hash, actor);
            }
            else hold.RecordRejection();
            db.StockReviews.Add(review); return null;
        }, ct);
    }
}
