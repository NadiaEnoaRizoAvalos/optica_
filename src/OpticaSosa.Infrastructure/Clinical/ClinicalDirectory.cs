using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Clinical;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Clinical;

public sealed class ClinicalDirectory(IDbContextFactory<OpticaDbContext> factory, ICurrentUser current) : IClinicalDirectory
{
    public async Task<CustomerClinicalOverview> CustomerAsync(Guid id, CancellationToken ct = default)
    {
        var actor = ClinicalOperations.Demand(current, Permissions.ReadClinical);
        await using var db = await factory.CreateDbContextAsync(ct);
        var records = db.ClinicalRecords.Where(x => x.CustomerId == id);
        var recipes = await records.CountAsync(x => x.Kind != ClinicalKind.VisualHistory, ct);
        var histories = await records.CountAsync(x => x.Kind == ClinicalKind.VisualHistory, ct);
        var revisions = from r in db.ClinicalRevisions join c in records on r.RecordId equals c.Id select r;
        var last = await revisions.MaxAsync(x => (DateOnly?)x.Date, ct);
        var professionals = await db.Professionals.AsNoTracking().Where(p => revisions.Any(r => r.ProfessionalId == p.Id))
            .OrderBy(p => p.FullName).Take(26).Select(p => new ProfessionalDetails(p.Id, p.FullName, p.Registration, p.Jurisdiction,
                p.IsActive, p.Version, p.Specialty, p.Phone, p.Email)).ToListAsync(ct);
        ClinicalOperations.Audit(db, actor, "clinical.customer.overview", id); await db.SaveChangesAsync(ct);
        return new(recipes, histories, last, professionals);
    }
    public async Task<ClinicalLinks> RecordsAsync(Guid? customerId = null, Guid? professionalId = null, bool? histories = null, int page = 1, CancellationToken ct = default)
    {
        var actor = ClinicalOperations.Demand(current, Permissions.ReadClinical);
        await using var db = await factory.CreateDbContextAsync(ct);
        // Para un emisor se conserva también su participación en versiones anteriores.
        var query = from c in db.ClinicalRecords.AsNoTracking()
                    join r in db.ClinicalRevisions on c.Id equals r.RecordId
                    join customer in db.Customers on c.CustomerId equals customer.Id
                    where (customerId == null || c.CustomerId == customerId) &&
                        (professionalId == null ? r.Number == c.RevisionNumber : r.ProfessionalId == professionalId &&
                            r.Number == db.ClinicalRevisions.Where(v => v.RecordId == c.Id && v.ProfessionalId == professionalId).Max(v => v.Number)) &&
                        (histories == null || (c.Kind == ClinicalKind.VisualHistory) == histories)
                    orderby r.Date descending, c.Id
                    select new ClinicalLink(c.Id, c.CustomerId, customer.LastName + ", " + customer.FirstName,
                        c.Kind == ClinicalKind.VisualHistory ? (r.IsFollowUp ? "Consulta / control" : "Antecedente visual") :
                        c.Kind == ClinicalKind.MedicalPrescription ? "Receta médica" : "Receta de óptica", r.Date, r.ProfessionalId,
                        r.ProfessionalName, r.Number, r.RecordedByUserId);
        var rows = await query.Skip(HistoryPaging.Offset(page)).Take(26).ToListAsync(ct);
        ClinicalOperations.Audit(db, actor, "clinical.directory.read", customerId ?? professionalId); await db.SaveChangesAsync(ct);
        return new(rows.Take(25).ToArray(), page, rows.Count > 25);
    }
}
