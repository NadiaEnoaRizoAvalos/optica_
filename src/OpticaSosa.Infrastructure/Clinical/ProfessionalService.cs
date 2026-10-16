using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Clinical;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Clinical;

public sealed class ProfessionalService(IDbContextFactory<OpticaDbContext> factory, ICurrentUser current,
    TimeProvider clock) : IProfessionalService
{
    private string Demand(string permission) => ClinicalOperations.Demand(current, permission);

    public async Task<IReadOnlyList<ProfessionalDetails>> SearchAsync(string? search, CancellationToken ct = default)
    {
        var actor = Demand(Permissions.ReadClinical);
        var key = (search ?? "").Trim().ToUpperInvariant();
        if (key.Length > 120) throw new ArgumentException("La búsqueda admite hasta 120 caracteres.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await db.Professionals.AsNoTracking().Where(x => key == "" || x.DuplicateKey.Contains(key) || (x.Specialty != null && x.Specialty.ToUpper().Contains(key)))
            .OrderByDescending(x => x.IsActive).ThenBy(x => x.FullName).ThenBy(x => x.Id).Take(100).ToListAsync(ct);
        Audit(db, actor, "professional.searched", null);
        await db.SaveChangesAsync(ct);
        return rows.Select(Details).ToArray();
    }

    public async Task<ProfessionalDetails?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var actor = Demand(Permissions.ReadClinical);
        await using var db = await factory.CreateDbContextAsync(ct);
        var professional = await db.Professionals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (professional is null) return null;
        Audit(db, actor, "professional.read", id);
        await db.SaveChangesAsync(ct);
        return Details(professional);
    }

    public Task<ClinicalResult> CreateAsync(ProfessionalInput input, CancellationToken ct = default) =>
        SaveAsync(null, input, ct);

    public Task<ClinicalResult> UpdateAsync(Guid id, ProfessionalInput input, CancellationToken ct = default) =>
        SaveAsync(id, input, ct);

    private async Task<ClinicalResult> SaveAsync(Guid? id, ProfessionalInput input, CancellationToken ct)
    {
        var actor = Demand(Permissions.WriteClinical);
        ProfessionalProfile profile;
        try { profile = input.ToProfile(); }
        catch (ArgumentException ex) { return new(ClinicalResultCode.Invalid, Error: ex.Message); }
        if (id == Guid.Empty || (id is null && (input.SubmissionId == Guid.Empty || !input.IsActive)))
            return new(ClinicalResultCode.Invalid, Error: "Recargá el formulario y registrá un profesional activo.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var targetId = id ?? input.SubmissionId;
        var row = await db.Professionals.FindAsync([targetId], ct);
        if (id is null && row is not null)
            return row.CreatedByUserId == actor && row.FullName == profile.FullName && row.Registration == profile.Registration &&
                row.Jurisdiction == profile.Jurisdiction && row.Specialty == profile.Specialty && row.Phone == profile.Phone && row.Email == profile.Email && row.IsActive
                ? new(ClinicalResultCode.Success, row.Id) : Conflict();
        if (id is not null && row is null) return new(ClinicalResultCode.NotFound, Error: "El profesional no existe.");
        if (row is not null && row.Version != input.Version) return Conflict();
        if (await db.Professionals.AnyAsync(x => x.Id != targetId && x.DuplicateKey == profile.DuplicateKey, ct))
            return new(ClinicalResultCode.Invalid, Error: "Ya existe un profesional con esos datos; revisá también los inactivos.");
        if (row is null)
        {
            row = new Professional(targetId, profile, actor, clock.GetUtcNow().UtcDateTime);
            db.Professionals.Add(row);
        }
        else row.Update(profile, input.IsActive);
        Audit(db, actor, id is null ? "professional.created" : "professional.updated", targetId);
        return await ClinicalOperations.SaveAsync(db, targetId, ct);
    }

    private static ClinicalResult Conflict() => ClinicalOperations.Conflict();
    private static ProfessionalDetails Details(Professional row) =>
        new(row.Id, row.FullName, row.Registration, row.Jurisdiction, row.IsActive, row.Version, row.Specialty, row.Phone, row.Email);
    private static void Audit(OpticaDbContext db, string actor, string action, Guid? target) =>
        ClinicalOperations.Audit(db, actor, action, target);
}
