using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Security;
using OpticaSosa.Application.Technical;
using OpticaSosa.Domain.Technical;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Technical;

public sealed partial class TechnicalPublicationService(IDbContextFactory<OpticaDbContext> factory, ICurrentUser current, TimeProvider clock)
    : ITechnicalPublicationService
{
    private string Demand(string permission) => current.Id is { Length: > 0 } actor && current.HasPermission(permission)
        ? actor : throw new UnauthorizedAccessException();

    public async Task<TechnicalPublicationPage> ListAsync(Guid customerId, Guid recordId, int page = 1, CancellationToken ct = default)
    {
        var actor = Demand(Permissions.ReadClinical);
        if (page < 1 || page > int.MaxValue / 50) throw new ArgumentOutOfRangeException(nameof(page));
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await (from publication in db.TechnicalPublications.AsNoTracking()
            join revision in db.ClinicalRevisions.AsNoTracking() on publication.SourceRevisionId equals revision.Id
            join record in db.ClinicalRecords.AsNoTracking() on revision.RecordId equals record.Id
            join customer in db.Customers.AsNoTracking() on record.CustomerId equals customer.Id
            where customer.Id == customerId && record.Id == recordId
            orderby publication.PublishedAtUtc descending, publication.Id descending
            select new { Publication = publication, record.CustomerId, RecordId = record.Id, revision.Number, CurrentNumber = record.RevisionNumber, customer.IsActive })
            .Skip((page - 1) * 50).Take(51).ToListAsync(ct);
        Audit(db, actor, "technical.publication.listed", recordId);
        await db.SaveChangesAsync(ct);
        return new(rows.Take(50).Select(x => Details(x.Publication, x.CustomerId, x.RecordId, x.Number, x.CurrentNumber, x.IsActive)).ToArray(), page, rows.Count > 50);
    }
    public async Task<TechnicalPublicationDetails?> GetAsync(Guid customerId, Guid publicationId, CancellationToken ct = default)
    {
        var actor = Demand(Permissions.ReadClinical);
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await (from publication in db.TechnicalPublications.AsNoTracking()
            join revision in db.ClinicalRevisions.AsNoTracking() on publication.SourceRevisionId equals revision.Id
            join record in db.ClinicalRecords.AsNoTracking() on revision.RecordId equals record.Id
            join customer in db.Customers.AsNoTracking() on record.CustomerId equals customer.Id
            where customer.Id == customerId && publication.Id == publicationId
            select new { Publication = publication, RecordId = record.Id, revision.Number, CurrentNumber = record.RevisionNumber, customer.IsActive }).SingleOrDefaultAsync(ct);
        if (row is null) return null;
        Audit(db, actor, "technical.publication.read", publicationId); await db.SaveChangesAsync(ct);
        return Details(row.Publication, customerId, row.RecordId, row.Number, row.CurrentNumber, row.IsActive);
    }
    private static TechnicalPublicationDetails Details(TechnicalPublication p, Guid customer, Guid record, int revision, int currentRevision, bool active)
    {
        var status = p.Status(revision, currentRevision, active);
        return new(p.Id, customer, record, p.SourceRevisionId, revision, p.Request, p.Right, p.Left, p.PupillaryDistanceHundredths / 100m,
            p.PublishedByUserId, p.PublishedAtUtc, p.Version, status, p.PreviousId, p.SupersededById, p.CorrectionReason, p.RevokedAtUtc, p.RevocationReason);
    }
}
