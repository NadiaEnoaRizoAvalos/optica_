using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Clinical;
using OpticaSosa.Domain.Customers;

namespace OpticaSosa.Infrastructure.Clinical;

public sealed partial class ClinicalService
{
    public Task<ClinicalResult> CreateAsync(Guid customerId, ClinicalInput input, CancellationToken ct = default) =>
        WriteAsync(customerId, null, input, ct);
    public Task<ClinicalResult> CorrectAsync(Guid customerId, Guid id, ClinicalInput input, CancellationToken ct = default) =>
        WriteAsync(customerId, id, input, ct);

    private async Task<ClinicalResult> WriteAsync(Guid customerId, Guid? id, ClinicalInput input, CancellationToken ct)
    {
        var actor = ClinicalOperations.Demand(current, Permissions.WriteClinical);
        if (input.SubmissionId == Guid.Empty) return ClinicalOperations.Invalid("Recargá el formulario.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var record = id is null ? null : await db.ClinicalRecords.SingleOrDefaultAsync(x => x.Id == id && x.CustomerId == customerId, ct);
        if (id is not null && record is null) return ClinicalOperations.Missing();
        var previous = await db.ClinicalRevisions.Include(x => x.Eyes).SingleOrDefaultAsync(x => x.Id == input.SubmissionId, ct);
        if (previous is not null)
        {
            var owner = await db.ClinicalRecords.AsNoTracking().SingleAsync(x => x.Id == previous.RecordId, ct);
            return owner.CustomerId == customerId && owner.Id == (id ?? input.SubmissionId) && owner.Kind == input.Kind &&
                owner.VisitId == input.VisitId && previous.RecordedByUserId == actor && previous.ToData() == input.ToData() &&
                (id is null || previous.CorrectionReason == input.CorrectionReason?.Trim())
                ? new(ClinicalResultCode.Success, owner.Id) : ClinicalOperations.Conflict();
        }
        if (record is not null && (record.Version != input.Version || record.Kind != input.Kind || record.VisitId != input.VisitId))
            return ClinicalOperations.Conflict();
        var customer = await db.Customers.FindAsync([customerId], ct);
        if (customer is null) return ClinicalOperations.Missing();
        if (id is null && !customer.IsActive) return ClinicalOperations.Invalid("El cliente está inactivo.");
        if (id is null && input.VisitId is not null && !await db.Visits.AnyAsync(x => x.Id == input.VisitId && x.CustomerId == customerId && x.Status != VisitStatus.Cancelled, ct))
            return ClinicalOperations.Invalid("La visita no corresponde al cliente o está cancelada.");
        var professional = await db.Professionals.FindAsync([input.ProfessionalId], ct);
        if (professional is null || !professional.IsActive) return ClinicalOperations.Invalid("Seleccioná un profesional activo.");
        try
        {
            if (record is null)
            {
                record = new ClinicalRecord(input.SubmissionId, customerId, input.VisitId, input.Kind);
                db.ClinicalRecords.Add(record);
            }
            var revision = new ClinicalRevision(input.SubmissionId, record, input.ToData(), professional, actor,
                clock.GetUtcNow().UtcDateTime, input.CorrectionReason);
            record.AddRevision();
            db.ClinicalRevisions.Add(revision);
        }
        catch (ArgumentException ex) { return ClinicalOperations.Invalid(ex.Message); }
        // Participan en la misma escritura para detectar inactivaciones/ediciones concurrentes.
        customer.Touch();
        professional.Touch();
        ClinicalOperations.Audit(db, actor, id is null ? "clinical.created" : "clinical.corrected", record.Id);
        return await ClinicalOperations.SaveAsync(db, record.Id, ct);
    }
}
