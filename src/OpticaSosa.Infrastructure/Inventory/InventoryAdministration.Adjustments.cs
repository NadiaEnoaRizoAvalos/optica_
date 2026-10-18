using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Inventory;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Infrastructure.Commerce;

namespace OpticaSosa.Infrastructure.Inventory;

public sealed partial class InventoryAdministration
{
    public Task<InventoryResult> AdjustAsync(Guid batchId, InventoryAdjustmentInput input, CancellationToken ct = default)
    {
        SalesOperations.Demand(current, Permissions.AdjustStock);
        return ExecuteAsync(batchId, input.SubmissionId, input.BatchVersion, "inventory.adjusted", StockMovementKind.Adjustment, input, async (db, batch, actor, hash) =>
        {
            if (input.Delta == 0 || input.Delta == int.MinValue) return Invalid("Indicá un ajuste no nulo dentro de la capacidad del inventario.");
            if (input.HoldId is not null && (!input.Quarantined || input.Delta > 0)) return Invalid("Sólo un retiro de cuarentena referencia un ingreso anterior.");
            if (input.Quarantined && input.Delta < 0) (await HoldAsync(db, batch, input.HoldId, input.HoldVersion, -input.Delta, ct)).Remove(-input.Delta);
            batch.Adjust(input.Delta, input.Quarantined ? input.Delta : 0);
            if (input.Quarantined && input.Delta > 0) db.StockHolds.Add(new(input.SubmissionId, batch.Id, StockHoldOrigin.Adjustment,
                input.Delta, false, input.Reason, actor, clock.GetUtcNow().UtcDateTime));
            Movement(db, input.SubmissionId, batch, null, StockMovementKind.Adjustment, input.Delta, input.Quarantined ? input.Delta : 0, input.Reason, hash, actor);
            return null;
        }, ct);
    }
    public Task<InventoryResult> QuarantineAsync(Guid batchId, InventoryQuarantineInput input, CancellationToken ct = default)
    {
        SalesOperations.Demand(current, Permissions.ReportStockIssue);
        return ExecuteAsync(batchId, input.SubmissionId, input.BatchVersion, "inventory.quarantined", StockMovementKind.Quarantine, input, (db, batch, actor, hash) =>
        {
            if (input.Quantity <= 0) return Task.FromResult<InventoryResult?>(Invalid("La cantidad debe ser positiva."));
            if (input.ContactPackageOpened is not null && batch.Category != OpticaSosa.Domain.Catalog.ProductCategory.ContactLens)
                return Task.FromResult<InventoryResult?>(Invalid("El estado del envase corresponde a contactos."));
            batch.Adjust(0, input.Quantity);
            db.StockHolds.Add(new(input.SubmissionId, batch.Id, StockHoldOrigin.Observation, input.Quantity, input.ContactPackageOpened == true, input.Reason, actor, clock.GetUtcNow().UtcDateTime));
            Movement(db, input.SubmissionId, batch, null, StockMovementKind.Quarantine, 0, input.Quantity, input.Reason, hash, actor);
            return Task.FromResult<InventoryResult?>(null);
        }, ct);
    }
    public Task<InventoryResult> WriteOffAsync(Guid batchId, InventoryWithdrawalInput input, CancellationToken ct = default) =>
        WithdrawAsync(batchId, input, StockMovementKind.WriteOff, ct);
    public Task<InventoryResult> ReturnToSupplierAsync(Guid batchId, InventoryWithdrawalInput input, CancellationToken ct = default) =>
        WithdrawAsync(batchId, input, StockMovementKind.SupplierReturn, ct);
    private Task<InventoryResult> WithdrawAsync(Guid batchId, InventoryWithdrawalInput input, StockMovementKind kind, CancellationToken ct)
    {
        SalesOperations.Demand(current, Permissions.AdjustStock);
        return ExecuteAsync(batchId, input.SubmissionId, input.BatchVersion, kind == StockMovementKind.WriteOff ? "inventory.written-off" : "inventory.returned-to-supplier",
            kind, input, async (db, batch, actor, hash) =>
        {
            if (input.Quantity <= 0) return Invalid("La cantidad debe ser positiva.");
            if (input.SupplierId is { } supplier && !await db.Suppliers.AnyAsync(x => x.Id == supplier, ct)) return Invalid("No se encontró el proveedor o laboratorio.");
            var withdrawal = new StockWithdrawal(input.SubmissionId, batch.Id, input.HoldId, input.SupplierId, input.Quantity, kind);
            if (input.HoldId is not null) (await HoldAsync(db, batch, input.HoldId, input.HoldVersion, input.Quantity, ct)).Remove(input.Quantity);
            batch.Adjust(-input.Quantity, input.HoldId is not null ? -input.Quantity : 0);
            db.StockWithdrawals.Add(withdrawal);
            Movement(db, input.SubmissionId, batch, null, kind, -input.Quantity, input.HoldId is not null ? -input.Quantity : 0, input.Reason, hash, actor);
            return null;
        }, ct);
    }
}
