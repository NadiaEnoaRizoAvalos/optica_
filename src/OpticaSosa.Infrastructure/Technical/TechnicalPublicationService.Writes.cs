using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Security;
using OpticaSosa.Application.Technical;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Clinical;
using OpticaSosa.Domain.Technical;
using OpticaSosa.Infrastructure.Identity;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Technical;

public sealed partial class TechnicalPublicationService
{
    public async Task<TechnicalResult> PublishAsync(Guid customerId, Guid recordId, TechnicalPublicationInput input, CancellationToken ct = default)
    {
        var actor = Demand(Permissions.WriteClinical);
        if (!input.Confirmed || input.SubmissionId == Guid.Empty || input.SourceRevisionId == Guid.Empty)
            return Invalid("Confirmá explícitamente la autorización y la versión de receta.");
        TechnicalRequest request;
        string? reason;
        try { request = input.ToRequest(); reason = input.CorrectsId is not null ? CatalogText.Required(input.CorrectionReason, 500, "el motivo de corrección") : null; }
        catch (ArgumentException ex) { return Invalid(ex.Message); }
        await using var db = await factory.CreateDbContextAsync(ct);
        var record = await db.ClinicalRecords.SingleOrDefaultAsync(x => x.Id == recordId && x.CustomerId == customerId, ct);
        if (record is null) return Missing();
        var existing = await db.TechnicalPublications.SingleOrDefaultAsync(x => x.Id == input.SubmissionId, ct);
        if (existing is not null)
            return existing.PublishedByUserId == actor && existing.SourceRevisionId == input.SourceRevisionId && existing.Request == request &&
                existing.PreviousId == input.CorrectsId && existing.CorrectionReason == reason &&
                await db.ClinicalRevisions.AnyAsync(x => x.Id == existing.SourceRevisionId && x.RecordId == record.Id, ct)
                ? new(TechnicalResultCode.Success, existing.Id) : Conflict();
        if (record.Version != input.RecordVersion) return Conflict();
        if (record.Kind == ClinicalKind.VisualHistory || !await db.Customers.AnyAsync(x => x.Id == customerId && x.IsActive, ct))
            return Invalid("Seleccioná una receta y un cliente activo; los antecedentes no autorizan fabricación.");
        var revision = await db.ClinicalRevisions.Include(x => x.Eyes).SingleOrDefaultAsync(x => x.Id == input.SourceRevisionId && x.RecordId == recordId, ct);
        if (revision is null) return Missing();
        if (revision.Number != record.RevisionNumber) return new(TechnicalResultCode.Obsolete, Error: "La receta tiene una versión posterior; revisala antes de autorizar.");
        TechnicalPublication publication;
        try { publication = new(input.SubmissionId, revision.Id, request, revision.ToData(), actor, clock.GetUtcNow().UtcDateTime, input.CorrectsId, reason); }
        catch (ArgumentException ex) { return Invalid(ex.Message); }
        if (input.CorrectsId is { } previousId)
        {
            var previous = await db.TechnicalPublications.SingleOrDefaultAsync(x => x.Id == previousId, ct);
            if (previous is null || !await db.ClinicalRevisions.AnyAsync(x => x.Id == previous.SourceRevisionId && x.RecordId == recordId, ct)) return Missing();
            if (previous.Version != input.CorrectsVersion || previous.SupersededById is not null) return Conflict();
            if (previous.Request.Purpose != request.Purpose) return Invalid("La corrección debe conservar el destino técnico; registrá otra publicación independiente.");
            previous.Supersede(publication.Id);
        }
        var referenceError = await ValidateReferencesAsync(db, publication, ct);
        if (referenceError is not null) return Invalid(referenceError);
        record.Touch(); db.TechnicalPublications.Add(publication);
        Audit(db, actor, input.CorrectsId is null ? "technical.publication.created" : "technical.publication.corrected", publication.Id);
        return await SaveAsync(db, publication.Id, ct);
    }

    public async Task<TechnicalResult> RevokeAsync(Guid customerId, Guid publicationId, Guid version, string reason, CancellationToken ct = default)
    {
        var actor = Demand(Permissions.WriteClinical);
        string normalized;
        try { normalized = CatalogText.Required(reason, 500, "el motivo de revocación"); }
        catch (ArgumentException ex) { return Invalid(ex.Message); }
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.TechnicalPublications.SingleOrDefaultAsync(x => x.Id == publicationId &&
            db.ClinicalRevisions.Any(r => r.Id == x.SourceRevisionId && db.ClinicalRecords.Any(c => c.Id == r.RecordId && c.CustomerId == customerId)), ct);
        if (row is null) return Missing();
        if (row.RevokedAtUtc is not null) return row.RevokedByUserId == actor && row.RevocationReason == normalized ? new(TechnicalResultCode.Success, row.Id) : Conflict();
        if (row.Version != version) return Conflict();
        row.Revoke(actor, clock.GetUtcNow().UtcDateTime, normalized);
        Audit(db, actor, "technical.publication.revoked", row.Id);
        return await SaveAsync(db, row.Id, ct);
    }

    private void Audit(OpticaDbContext db, string actor, string action, Guid target) => db.AuditEntries.Add(new AuditEntry
        { ActorId = actor, Action = action, TargetId = target.ToString(), OccurredAt = clock.GetUtcNow() });
    private static TechnicalResult Invalid(string error) => new(TechnicalResultCode.Invalid, Error: error);
    private static TechnicalResult Missing() => new(TechnicalResultCode.NotFound, Error: "No se encontró la publicación o receta para este cliente.");
    private static TechnicalResult Conflict() => new(TechnicalResultCode.Conflict, Error: "Los datos cambiaron; recargá antes de autorizar o revocar.");
    private static async Task<TechnicalResult> SaveAsync(OpticaDbContext db, Guid id, CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return new(TechnicalResultCode.Success, id); }
        catch (DbUpdateConcurrencyException) { return Conflict(); }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 or 5 or 6 }) { return Conflict(); }
        catch (SqliteException ex) when (ex.SqliteErrorCode is 5 or 6) { return Conflict(); }
    }
}
