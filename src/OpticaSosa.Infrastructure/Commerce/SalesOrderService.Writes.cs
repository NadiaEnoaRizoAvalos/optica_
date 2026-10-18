using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Commerce;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Commerce;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Commerce;

public sealed partial class SalesOrderService
{
    public Task<SalesResult> CreateDraftAsync(SalesDraftInput input, CancellationToken ct = default) => SaveDraftAsync(null, input, ct);
    public Task<SalesResult> UpdateDraftAsync(Guid orderId, SalesDraftInput input, CancellationToken ct = default) => SaveDraftAsync(orderId, input, ct);
    private Task<SalesResult> SaveDraftAsync(Guid? orderId, SalesDraftInput input, CancellationToken ct)
    {
        var actor = SalesOperations.Demand(current, Permissions.WriteSales);
        var target = orderId ?? input.SubmissionId;
        return SalesOperations.ExecuteAsync(factory, clock, actor, target, input.SubmissionId,
            orderId is null ? "sales.draft.created" : "sales.draft.updated", input, async db =>
        {
            if (input.Lines is null || input.Lines.Any(x => x is null)) return SalesOperations.Invalid("Revisá las líneas del pedido.");
            var row = await db.SalesOrders.SingleOrDefaultAsync(x => x.Id == target, ct);
            if (orderId is not null && row is null) return SalesOperations.Missing();
            if (orderId is null && row is not null) return SalesOperations.Conflict();
            if (row is not null && row.Version != input.Version) return SalesOperations.Conflict();
            var currency = Money.CurrencyCode(input.Currency);
            if (row is not null && (row.CustomerId != input.CustomerId || row.Currency != currency))
                return SalesOperations.Invalid("Cliente y moneda del pedido no pueden sustituirse; creá otro borrador.");
            if (!await db.Customers.AnyAsync(x => x.Id == input.CustomerId && x.IsActive, ct))
                return SalesOperations.Invalid("Seleccioná un cliente activo.");
            if (row is null)
            {
                var configuration = await db.CommercialSettings.SingleAsync(ct);
                if (configuration.Currency != currency) return SalesOperations.Invalid("La moneda debe coincidir con la configuración comercial.");
                row = new(target, input.CustomerId, currency, actor, clock.GetUtcNow().UtcDateTime);
                db.SalesOrders.Add(row);
            }
            if (row.Status != SalesOrderStatus.Draft) return SalesOperations.Invalid("Sólo se pueden editar borradores.");
            var ids = input.Lines.Select(x => x.VariantId).Distinct().ToArray();
            var variants = await db.ProductVariants.Where(x => ids.Contains(x.Id)).ToListAsync(ct);
            var productIds = variants.Select(x => x.ProductId).Distinct().ToArray();
            var products = await db.Products.Where(x => productIds.Contains(x.Id)).ToListAsync(ct);
            var nextDraft = checked(row.DraftNumber + 1);
            long total = 0; var position = 0;
            foreach (var item in input.Lines)
            {
                var variant = variants.SingleOrDefault(x => x.Id == item.VariantId);
                var product = products.SingleOrDefault(x => x.Id == variant?.ProductId);
                if (variant is null || product is null || !variant.IsActive || !product.IsActive)
                    return SalesOperations.Invalid("Todas las variantes y productos deben estar activos.");
                if (variant.Version != item.VariantVersion) return SalesOperations.Conflict();
                var line = new SalesLine(Guid.NewGuid(), target, nextDraft, ++position, variant, product.Name,
                    item.Quantity, item.Source, Money.FromDecimal(item.UnitPrice, currency), Money.FromDecimal(item.Discount, currency), item.DiscountReason);
                total = checked(total + line.TotalMinor); db.SalesLines.Add(line);
            }
            foreach (var variant in variants) variant.LockSpecification();
            foreach (var product in products) product.Touch();
            row.Revise(total, input.PromisedDate);
            return null;
        }, ct);
    }

    public Task<SalesResult> ConfirmAsync(Guid orderId, OrderActionInput input, CancellationToken ct = default)
    {
        var actor = SalesOperations.Demand(current, Permissions.WriteSales);
        return SalesOperations.ExecuteAsync(factory, clock, actor, orderId, input.SubmissionId, "sales.order.confirmed", input, async db =>
        {
            var row = await db.SalesOrders.SingleOrDefaultAsync(x => x.Id == orderId, ct);
            if (row is null) return SalesOperations.Missing();
            if (row.Version != input.Version) return SalesOperations.Conflict();
            if (!await db.Customers.AnyAsync(x => x.Id == row.CustomerId && x.IsActive, ct)) return SalesOperations.Invalid("El cliente está inactivo.");
            var lines = await db.SalesLines.Where(x => x.OrderId == orderId && x.DraftNumber == row.DraftNumber).ToListAsync(ct);
            var variantIds = lines.Select(x => x.VariantId).Distinct().ToArray();
            var activeCount = await db.ProductVariants.CountAsync(x => variantIds.Contains(x.Id) && x.IsActive && db.Products.Any(p => p.Id == x.ProductId && p.IsActive), ct);
            if (activeCount != variantIds.Length) return SalesOperations.Invalid("Hay productos o variantes inactivos; corregí el borrador.");
            row.Confirm(actor, clock.GetUtcNow().UtcDateTime, lines.Count);
            // Confirmar no publica graduaciones, reserva existencias ni envía encargos. La seña puede preceder la técnica.
            return null;
        }, ct);
    }

    public Task<SalesResult> CancelAsync(Guid orderId, CancelOrderInput input, CancellationToken ct = default)
    {
        var actor = SalesOperations.Demand(current, Permissions.WriteSales);
        return SalesOperations.ExecuteAsync(factory, clock, actor, orderId, input.SubmissionId, "sales.order.cancelled", input, async db =>
        {
            var row = await db.SalesOrders.SingleOrDefaultAsync(x => x.Id == orderId, ct);
            if (row is null) return SalesOperations.Missing();
            if (row.Version != input.Version) return SalesOperations.Conflict();
            if (row.ReceivedMinor > 0 && !current.HasPermission(Permissions.RefundPayments))
                throw new UnauthorizedAccessException("Sólo Administrador puede cancelar un pedido que recibió pagos.");
            if (await db.StockReservations.AnyAsync(x => x.Status != OpticaSosa.Domain.Inventory.ReservationStatus.Released &&
                db.SalesLines.Any(l => l.Id == x.LineId && l.OrderId == orderId), ct))
                return SalesOperations.Invalid("Liberá explícitamente las reservas antes de cancelar; el material consumido requiere devolución.");
            row.Cancel(actor, clock.GetUtcNow().UtcDateTime, input.Reason);
            return null;
        }, ct);
    }
}
