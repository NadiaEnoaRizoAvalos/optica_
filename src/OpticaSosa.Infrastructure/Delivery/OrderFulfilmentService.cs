using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Application.Commerce;
using OpticaSosa.Application.Delivery;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Commerce;
using OpticaSosa.Domain.Delivery;
using OpticaSosa.Domain.Work;
using OpticaSosa.Infrastructure.Commerce;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Delivery;

public static class DeliveryRegistration
{
    public static IServiceCollection AddDelivery(this IServiceCollection services) => services.AddScoped<IOrderFulfilmentService, OrderFulfilmentService>();
}

public sealed partial class OrderFulfilmentService(IDbContextFactory<OpticaDbContext> factory, ICurrentUser current, TimeProvider clock) : IOrderFulfilmentService
{
    private string Read() => current.Id is { Length: > 0 } actor &&
        (current.HasPermission(Permissions.ReadOrders) || current.HasPermission(Permissions.ReadSales)) ? actor : throw new UnauthorizedAccessException();

    public Task<SalesResult> CreateWorkAsync(Guid orderId, CommercialWorkInput input, CancellationToken ct = default)
    {
        var actor = SalesOperations.Demand(current, Permissions.WriteOrders);
        return SalesOperations.ExecuteAsync(factory, clock, actor, orderId, input.SubmissionId, "delivery.work.created", input, async db =>
        {
            var order = await db.SalesOrders.SingleOrDefaultAsync(x => x.Id == orderId, ct);
            if (order is null) return SalesOperations.Missing();
            if (order.Version != input.OrderVersion) return SalesOperations.Conflict();
            if (order.Status != SalesOrderStatus.Confirmed || !await db.Customers.AnyAsync(x => x.Id == order.CustomerId && x.IsActive, ct))
                return SalesOperations.Invalid("Seleccioná un pedido confirmado de cliente activo.");
            if (input.Materials is null || input.Materials.Count == 0 || input.Materials.Any(x => x is null) || input.Materials.Select(x => x.LineId).Distinct().Count() != input.Materials.Count)
                return SalesOperations.Invalid("Seleccioná materiales explícitos sin duplicarlos.");
            var lineIds = input.Materials.Select(x => x.LineId).ToArray();
            var lines = await db.SalesLines.Where(x => lineIds.Contains(x.Id) && x.OrderId == orderId && x.DraftNumber == order.DraftNumber).ToListAsync(ct);
            if (lines.Count != lineIds.Length) return SalesOperations.Invalid("Los materiales deben pertenecer al pedido confirmado.");
            var categories = lines.Select(x => x.Category).Distinct().ToArray();
            if (!categories.Contains(ProductCategory.Lens) && lines.Count != 1)
                return SalesOperations.Invalid("Los productos independientes requieren sus propios trabajos.");
            if (lines.Count(x => x.Category == ProductCategory.Frame) > 1)
                return SalesOperations.Invalid("Seleccioná el armazón de un solo conjunto; no agrupar armazones independientes automáticamente.");
            var type = categories.Contains(ProductCategory.Lens) ? OpticalWorkType.CommercialGlasses : (categories.Length == 1 ? categories[0] : (ProductCategory)0) switch
            {
                ProductCategory.ContactLens => OpticalWorkType.ContactLenses, ProductCategory.Frame => OpticalWorkType.CommercialFrame,
                ProductCategory.Accessory => OpticalWorkType.Accessory, _ => (OpticalWorkType)(-1)
            };
            if ((type == OpticalWorkType.CommercialGlasses && categories.Any(x => x is not (ProductCategory.Lens or ProductCategory.Frame))) || !Enum.IsDefined(type))
                return SalesOperations.Invalid("Separá contactos y accesorios de los conjuntos de anteojos.");
            var bindings = input.Materials.Select(x => new CommercialWorkLine(input.SubmissionId, x.LineId, x.TechnicalAssignmentId)).ToList();
            var error = await CommercialWorkRules.TechnicalAsync(db, order, bindings, ct);
            if (error is not null) return SalesOperations.Invalid(error);
            if (input.PreviousWorkId is { } previous)
            {
                var group = await db.Set<CommercialWork>().SingleOrDefaultAsync(x => x.WorkId == previous && x.OrderId == orderId && x.IsCurrent, ct);
                var old = await db.OpticalWorks.SingleOrDefaultAsync(x => x.Id == previous, ct);
                if (group is null || old is null || old.Version != input.PreviousWorkVersion || old.Status == WorkStatus.Delivered) return SalesOperations.Conflict();
                CatalogText.Required(input.ReplacementReason, 500, "el motivo de reemplazo");
                foreach (var binding in await db.Set<CommercialWorkLine>().Where(x => x.WorkId == previous && x.IsCurrent).ToListAsync(ct)) binding.Retire();
                var from = old.Status; group.Retire(); old.Invalidate();
                db.WorkTransitions.Add(new(old, from, actor, clock.GetUtcNow().UtcDateTime, "delivery.work.replaced", input.ReplacementReason));
                await db.SaveChangesAsync(ct); // Liberar el índice único dentro de la misma transacción.
            }
            if (await db.Set<CommercialWorkLine>().AnyAsync(x => lineIds.Contains(x.LineId) && x.IsCurrent, ct))
                return SalesOperations.Invalid("Una línea ya pertenece a otro trabajo; reemplazá explícitamente el conjunto.");
            Guid? revisionId = null; Guid? recordId = null;
            var assignmentId = bindings.FirstOrDefault(x => x.TechnicalAssignmentId is not null)?.TechnicalAssignmentId;
            if (assignmentId is not null)
            {
                var reference = await (from a in db.OrderTechnicalAssignments join p in db.TechnicalPublications on a.PublicationId equals p.Id
                    join r in db.ClinicalRevisions on p.SourceRevisionId equals r.Id where a.Id == assignmentId select new { r.Id, r.RecordId }).SingleAsync(ct);
                revisionId = reference.Id; recordId = reference.RecordId;
            }
            var frame = string.IsNullOrWhiteSpace(input.FrameReference) ? lines.FirstOrDefault(x => x.Category == ProductCategory.Frame)?.VariantCode ?? "" : input.FrameReference;
            var work = new OpticalWork(input.SubmissionId, order.CustomerId, type,
                type == OpticalWorkType.CommercialGlasses ? "Conjunto de anteojos" : lines[0].ProductName, frame, recordId, actor, clock.GetUtcNow().UtcDateTime, revisionId);
            db.OpticalWorks.Add(work); db.Set<CommercialWork>().Add(new(work.Id, order.Id, input.PreviousWorkId, input.ReplacementReason));
            db.Set<CommercialWorkLine>().AddRange(bindings);
            db.WorkTransitions.Add(new(work, WorkStatus.Registered, actor, clock.GetUtcNow().UtcDateTime, "delivery.work.created"));
            order.RegisterOperationalChange(); return null;
        }, ct);
    }

    public Task<SalesResult> ReopenAsync(Guid orderId, Guid workId, ReopenWorkInput input, CancellationToken ct = default)
    {
        var actor = SalesOperations.Demand(current, Permissions.WriteOrders);
        return SalesOperations.ExecuteAsync(factory, clock, actor, orderId, input.SubmissionId, "delivery.work.reopened", new { workId, input }, async db =>
        {
            CatalogText.Required(input.Reason, 500, "el motivo de nueva revisión");
            var order = await db.SalesOrders.SingleOrDefaultAsync(x => x.Id == orderId, ct);
            var group = await db.Set<CommercialWork>().SingleOrDefaultAsync(x => x.WorkId == workId && x.OrderId == orderId && x.IsCurrent, ct);
            var work = await db.OpticalWorks.SingleOrDefaultAsync(x => x.Id == workId, ct);
            if (order is null || group is null || work is null) return SalesOperations.Missing();
            if (order.Version != input.OrderVersion || work.Version != input.WorkVersion || work.Status == WorkStatus.Delivered) return SalesOperations.Conflict();
            var from = work.Status; work.Invalidate(); group.ClearControls(); order.RegisterOperationalChange();
            db.WorkTransitions.Add(new(work, from, actor, clock.GetUtcNow().UtcDateTime, "delivery.work.reopened", input.Reason));
            return null;
        }, ct);
    }
}
