using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Work;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Work;
using OpticaSosa.Domain.Clinical;
using OpticaSosa.Infrastructure.Clinical;
using OpticaSosa.Domain.Delivery;
using OpticaSosa.Infrastructure.Delivery;

namespace OpticaSosa.Infrastructure.Work;

public sealed partial class WorkService
{
    public async Task<ClinicalResult> CreateAsync(WorkInput input, CancellationToken ct = default)
    {
        var actor = ClinicalOperations.Demand(current, Permissions.WriteOrders);
        await using var db = await factory.CreateDbContextAsync(ct);
        if (!await db.Customers.AnyAsync(x => x.Id == input.CustomerId && x.IsActive, ct)) return ClinicalOperations.Invalid("Seleccioná un cliente activo.");
        if (!await ValidPrescriptionAsync(db, input.CustomerId, input.PrescriptionId, ct)) return ClinicalOperations.Invalid("La receta no corresponde al cliente o no tenés permiso para vincularla.");
        var old = await db.OpticalWorks.FindAsync([input.SubmissionId], ct);
        if (old is not null) return old.CreatedByUserId == actor && old.CustomerId == input.CustomerId && old.Product == input.Product.Trim() &&
            old.Frame == input.Frame.Trim() && old.Type == input.Type && old.PrescriptionId == input.PrescriptionId
            ? new(ClinicalResultCode.Success, old.Id) : ClinicalOperations.Conflict();
        try
        {
            var revisionId = await PrescriptionRevisionAsync(db, input.PrescriptionId, ct);
            var work = new OpticalWork(input.SubmissionId, input.CustomerId, input.Type, input.Product, input.Frame, input.PrescriptionId, actor, clock.GetUtcNow().UtcDateTime, revisionId);
            db.Add(work); Event(db, work, WorkStatus.Registered, actor, "work.created");
            return await ClinicalOperations.SaveAsync(db, work.Id, ct);
        }
        catch (ArgumentException ex) { return ClinicalOperations.Invalid(ex.Message); }
    }
    public async Task<ClinicalResult> EditAsync(Guid id, WorkInput input, CancellationToken ct = default)
    {
        var actor = ClinicalOperations.Demand(current, Permissions.WriteOrders);
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var work = await db.OpticalWorks.FindAsync([id], ct);
        if (work is null) return ClinicalOperations.Missing();
        if (await db.Set<CommercialWork>().AnyAsync(x => x.WorkId == id, ct))
            return ClinicalOperations.Invalid("El trabajo pertenece a un pedido comercial; reemplazá explícitamente su conjunto para cambiar especificaciones.");
        if (work.Version != input.Version || work.CustomerId != input.CustomerId || work.Type != input.Type) return ClinicalOperations.Conflict();
        // Atención/Ventas puede rectificar datos operativos, pero no alterar el vínculo clínico.
        var prescription = current.HasPermission(Permissions.ReadClinical) ? input.PrescriptionId : work.PrescriptionId;
        if (!current.HasPermission(Permissions.ReadClinical) && input.PrescriptionId is not null) throw new UnauthorizedAccessException();
        if (current.HasPermission(Permissions.ReadClinical) && !await ValidPrescriptionAsync(db, work.CustomerId, prescription, ct))
            return ClinicalOperations.Invalid("La receta debe pertenecer al cliente.");
        var from = work.Status;
        var revision = current.HasPermission(Permissions.ReadClinical) ? await PrescriptionRevisionAsync(db, prescription, ct) : work.PrescriptionRevisionId;
        try { work.Edit(input.Product, input.Frame, prescription, revision); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return ClinicalOperations.Invalid(ex.Message); }
        if (work.Version == input.Version) return new(ClinicalResultCode.Success, id);
        Event(db, work, from, actor, "work.updated-controls-invalidated");
        var saved = await ClinicalOperations.SaveAsync(db, id, ct);
        if (saved.Succeeded) await transaction.CommitAsync(ct);
        return saved;
    }
    public async Task<ClinicalResult> TransitionAsync(Guid id, Guid version, string command, CancellationToken ct = default)
    {
        var actor = ClinicalOperations.Demand(current, command == "deliver" ? Permissions.DeliverOrders : Permissions.WriteOrders);
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var work = await db.OpticalWorks.FindAsync([id], ct);
        if (work is null) return ClinicalOperations.Missing();
        if (work.Version != version) return ClinicalOperations.Conflict();
        var commercial = await CommercialWorkRules.ForWorkAsync(db, id, clock, false, ct);
        if (commercial.Group is not null)
        {
            if (command == "deliver") return ClinicalOperations.Invalid("Registrá la entrega comercial completa del pedido, no un trabajo individual.");
            if (!commercial.Readiness.Ready) return ClinicalOperations.Invalid(commercial.Readiness.Error!);
        }
        var from = work.Status;
        try { work.Transition(command); }
        catch (InvalidOperationException ex) { return ClinicalOperations.Invalid(ex.Message); }
        Event(db, work, from, actor, command == "deliver" ? "work.delivered" : "work.state.changed");
        var saved = await ClinicalOperations.SaveAsync(db, id, ct);
        if (saved.Succeeded) await transaction.CommitAsync(ct);
        return saved;
    }
    private async Task<bool> ValidPrescriptionAsync(OpticaSosa.Infrastructure.Persistence.OpticaDbContext db, Guid customerId, Guid? prescriptionId, CancellationToken ct)
    {
        if (prescriptionId is null) return true;
        if (!current.HasPermission(Permissions.ReadClinical)) return false;
        return await db.ClinicalRecords.AnyAsync(x => x.Id == prescriptionId && x.CustomerId == customerId && x.Kind != ClinicalKind.VisualHistory, ct);
    }
    private static Task<Guid?> PrescriptionRevisionAsync(OpticaSosa.Infrastructure.Persistence.OpticaDbContext db, Guid? id, CancellationToken ct) =>
        (from r in db.ClinicalRecords join v in db.ClinicalRevisions on r.Id equals v.RecordId
         where r.Id == id && r.RevisionNumber == v.Number select (Guid?)v.Id).SingleOrDefaultAsync(ct);
}
