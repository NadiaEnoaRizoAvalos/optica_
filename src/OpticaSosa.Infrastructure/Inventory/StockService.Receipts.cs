using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Inventory;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Infrastructure.Commerce;

namespace OpticaSosa.Infrastructure.Inventory;

public sealed partial class StockService
{
    internal static string Hash(object input) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input)));
    public async Task<StockReceiptResult> ReceiveAsync(StockReceiptInput input, CancellationToken ct = default)
    {
        var actor = SalesOperations.Demand(current, Permissions.ReceiveStock);
        if (input.SubmissionId == Guid.Empty) return new(false, Error: "Recargá el formulario de recepción.");
        var hash = Hash(input);
        await using var db = await factory.CreateDbContextAsync(ct);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var prior = await db.StockMovements.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.SubmissionId, ct);
            if (prior is not null) return prior.Kind == StockMovementKind.Receipt && prior.ActorUserId == actor && prior.RequestHash == hash
                ? new(true, prior.BatchId) : new(false, Error: "El identificador ya corresponde a otro movimiento.", Conflict: true);
            var variant = await db.ProductVariants.SingleOrDefaultAsync(x => x.Id == input.VariantId && x.IsActive, ct);
            if (variant is null || !await db.Products.AnyAsync(x => x.Id == variant.ProductId && x.IsActive, ct))
                return new(false, Error: "Seleccioná una variante y producto activos.");
            if (variant.Version != input.VariantVersion) return new(false, Error: "La variante cambió; recargala.", Conflict: true);
            var batch = await InventoryReceiptWriter.AddAsync(db, variant, input, actor, clock, ct);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return new(true, batch.Id);
        }
        catch (ArgumentException ex) { return new(false, Error: ex.Message); }
        catch (DbUpdateConcurrencyException) { return ReceiptConflict(); }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 or 5 or 6 }) { return ReceiptConflict(); }
        catch (SqliteException ex) when (ex.SqliteErrorCode is 5 or 6) { return ReceiptConflict(); }
    }
    private static StockReceiptResult ReceiptConflict() => new(false, Error: "La recepción cambió o está ocupada; recargá y reintentá sin duplicar el identificador.", Conflict: true);
}
