using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Work;
using OpticaSosa.Application.Security;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Domain.Work;
using OpticaSosa.Infrastructure.Persistence;
using OpticaSosa.Infrastructure.Clinical;

namespace OpticaSosa.Infrastructure.Work;

public sealed partial class WorkService(IDbContextFactory<OpticaDbContext> factory, ICurrentUser current, TimeProvider clock) : IWorkService
{
    public async Task<WorkPage> ListAsync(Guid? customerId = null, WorkStatus? status = null, int page = 1, CancellationToken ct = default)
    {
        var actor = ClinicalOperations.Demand(current, Permissions.ReadOrders);
        if (status.HasValue && !Enum.IsDefined(status.Value)) throw new ArgumentException("Estado inválido.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var query = db.OpticalWorks.AsNoTracking().Where(x => (customerId == null || x.CustomerId == customerId) && (status == null || x.Status == status))
            .OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(HistoryPaging.Offset(page)).Take(26);
        var rows = await Summaries(db, query).ToListAsync(ct);
        ClinicalOperations.Audit(db, actor, "work.list.read", customerId); await db.SaveChangesAsync(ct);
        return new(rows.Take(25).ToArray(), page, rows.Count > 25);
    }
    public async Task<int> CountAsync(Guid customerId, CancellationToken ct = default)
    {
        ClinicalOperations.Demand(current, Permissions.ReadOrders);
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.OpticalWorks.CountAsync(x => x.CustomerId == customerId, ct);
    }
    public async Task<WorkDetails?> GetAsync(Guid id, int page = 1, CancellationToken ct = default)
    {
        var actor = ClinicalOperations.Demand(current, Permissions.ReadOrders);
        await using var db = await factory.CreateDbContextAsync(ct);
        var work = await Summaries(db, db.OpticalWorks.AsNoTracking().Where(x => x.Id == id)).SingleOrDefaultAsync(ct);
        if (work is null) return null;
        var clinical = current.HasPermission(Permissions.ReadClinical);
        var visibleTechnical = clinical || !QualityCatalog.RequiresProfessional(work.Type);
        var controlsQuery = db.QualityControls.AsNoTracking().Where(x => x.WorkId == id && (visibleTechnical || x.Stage == QualityStage.Delivery));
        var count = await controlsQuery.CountAsync(ct);
        var controls = await controlsQuery.Include(x => x.Items).OrderByDescending(x => x.AtUtc).ThenByDescending(x => x.Id)
            .Skip(HistoryPaging.Offset(page)).Take(26).ToListAsync(ct);
        var transitions = await db.WorkTransitions.AsNoTracking().Where(x => x.WorkId == id).OrderByDescending(x => x.AtUtc).ThenByDescending(x => x.Id)
            .Skip(HistoryPaging.Offset(page)).Take(26).Select(x => new TransitionDetails(x.From, x.To, x.ActorId, x.AtUtc, x.Action)).ToListAsync(ct);
        Guid? prescription = clinical ? await db.OpticalWorks.Where(x => x.Id == id).Select(x => x.PrescriptionId).SingleAsync(ct) : null;
        int? revision = clinical ? await (from w in db.OpticalWorks join r in db.ClinicalRevisions on w.PrescriptionRevisionId equals r.Id where w.Id == id select (int?)r.Number).SingleOrDefaultAsync(ct) : null;
        ClinicalOperations.Audit(db, actor, "work.read", id); await db.SaveChangesAsync(ct);
        return new(work, prescription, count, controls.Take(25).Select(x => new ControlDetails(x.Id, x.Round, x.Stage, x.Status,
            x.ActorId, x.Role, x.AtUtc, x.ApprovedAtUtc, x.Notes, x.Items.Select(i => new CheckDetails(i.Key, i.Label, i.Applicable, i.Required, i.Status, i.Notes)).ToArray())).ToArray(),
            transitions.Take(25).ToArray(), page, controls.Count > 25, transitions.Count > 25, revision);
    }
    private static IQueryable<WorkSummary> Summaries(OpticaDbContext db, IQueryable<OpticalWork> works) =>
        from w in works join c in db.Customers on w.CustomerId equals c.Id
        select new WorkSummary(w.Id, w.CustomerId, c.LastName + ", " + c.FirstName, w.Product, w.Frame, w.Type, w.Status,
            w.CreatedAtUtc, w.Version, w.Round, w.TechnicalApproved, w.DeliveryApproved);
    private void Event(OpticaDbContext db, OpticalWork work, WorkStatus from, string actor, string action)
    {
        db.WorkTransitions.Add(new(work, from, actor, clock.GetUtcNow().UtcDateTime, action));
        ClinicalOperations.Audit(db, actor, action, work.Id);
    }
}
