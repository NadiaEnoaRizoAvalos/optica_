using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Clinical;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Clinical;

public sealed partial class ClinicalService(IDbContextFactory<OpticaDbContext> factory, ICurrentUser current,
    TimeProvider clock) : IClinicalService
{
    public async Task<ClinicalHistory> ListAsync(Guid customerId, int page = 1, CancellationToken ct = default)
    {
        var actor = ClinicalOperations.Demand(current, Permissions.ReadClinical);
        if (page < 1 || page > 100000) throw new ArgumentException("Página inválida.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await (from record in db.ClinicalRecords.AsNoTracking()
                          join revision in db.ClinicalRevisions.AsNoTracking() on record.Id equals revision.RecordId
                          where record.CustomerId == customerId && record.RevisionNumber == revision.Number
                          orderby revision.Date descending, record.Id
                          select new ClinicalSummary(record.Id, record.Kind, revision.Date, revision.ProfessionalName, record.RevisionNumber))
            .Skip((page - 1) * 25).Take(26).ToListAsync(ct);
        ClinicalOperations.Audit(db, actor, "clinical.history.read", customerId);
        await db.SaveChangesAsync(ct);
        return new(rows.Take(25).ToArray(), page, rows.Count > 25);
    }

    public async Task<ClinicalDetails?> GetAsync(Guid customerId, Guid id, int? revision = null, int historyPage = 1, CancellationToken ct = default)
    {
        var actor = ClinicalOperations.Demand(current, Permissions.ReadClinical);
        if (historyPage < 1 || historyPage > 100000) throw new ArgumentException("Página inválida.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var record = await db.ClinicalRecords.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.CustomerId == customerId, ct);
        if (record is null) return null;
        var number = revision ?? record.RevisionNumber;
        var row = await db.ClinicalRevisions.AsNoTracking().Include(x => x.Eyes)
            .SingleOrDefaultAsync(x => x.RecordId == id && x.Number == number, ct);
        if (row is null) return null;
        var history = await db.ClinicalRevisions.AsNoTracking().Where(x => x.RecordId == id).OrderByDescending(x => x.Number)
            .Skip((historyPage - 1) * 25).Take(26)
            .Select(x => new RevisionSummary(x.Number, x.RecordedAtUtc, x.RecordedByUserId, x.CorrectionReason)).ToListAsync(ct);
        ClinicalOperations.Audit(db, actor, "clinical.revision.read", row.Id);
        await db.SaveChangesAsync(ct);
        return new(record.Id, customerId, record.VisitId, record.Kind, record.Version, record.RevisionNumber, row.Id,
            row.Number, row.ToData(), row.ProfessionalName, row.ProfessionalRegistration, row.ProfessionalJurisdiction,
            row.RecordedByUserId, row.RecordedAtUtc, row.CorrectionReason, history.Take(25).ToArray(), historyPage, history.Count > 25);
    }
}
