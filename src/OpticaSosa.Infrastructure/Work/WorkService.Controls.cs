using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Work;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Work;
using OpticaSosa.Infrastructure.Persistence;
using OpticaSosa.Infrastructure.Clinical;

namespace OpticaSosa.Infrastructure.Work;

public sealed partial class WorkService
{
    public async Task<TemplateDetails> TemplateAsync(OpticalWorkType type, QualityStage stage, CancellationToken ct = default)
    {
        if (current.Id is null || (!current.HasPermission(Permissions.ReadOrders) && !current.HasPermission(Permissions.ConfigureQuality))) throw new UnauthorizedAccessException();
        await using var db = await factory.CreateDbContextAsync(ct);
        return await ReadTemplateAsync(db, type, stage, ct);
    }
    private static async Task<TemplateDetails> ReadTemplateAsync(OpticaDbContext db, OpticalWorkType type, QualityStage stage, CancellationToken ct)
    {
        var rules = QualityCatalog.For(type, stage);
        var template = await db.QualityTemplates.AsNoTracking().Include(x => x.Items).SingleOrDefaultAsync(x => x.Type == type && x.Stage == stage, ct);
        return new(type, stage, template?.Version ?? Guid.Empty,
            rules.Select(x => x with { Required = x.Essential || (template?.Items.SingleOrDefault(i => i.Key == x.Key)?.Required ?? x.Required) }).ToArray());
    }
    public async Task<ClinicalResult> SaveControlAsync(Guid id, ControlInput input, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(input.Stage)) return ClinicalOperations.Invalid("Tipo de control inválido.");
        var actor = ClinicalOperations.Demand(current, input.Stage == QualityStage.Technical ? Permissions.TechnicalControl : Permissions.DeliveryControl);
        if (input.Approve) ClinicalOperations.Demand(current, input.Stage == QualityStage.Technical ? Permissions.ApproveTechnical : Permissions.ApproveDelivery);
        await using var db = await factory.CreateDbContextAsync(ct);
        var responsibleRoles = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id
                                      join u in db.Users on ur.UserId equals u.Id where u.Id == actor && u.IsActive select r.Name).ToListAsync(ct);
        var role = responsibleRoles.Contains(Roles.Professional) ? ResponsibleRole.Professional : ResponsibleRole.Sales;
        if (!responsibleRoles.Any(x => x is Roles.Professional or Roles.Sales) || input.Stage == QualityStage.Technical && role != ResponsibleRole.Professional)
            throw new UnauthorizedAccessException();
        var work = await db.OpticalWorks.FindAsync([id], ct);
        if (work is null) return ClinicalOperations.Missing();
        var previous = await db.QualityControls.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == input.SubmissionId, ct);
        if (previous is not null)
            return previous.WorkId == id && previous.ActorId == actor && previous.Stage == input.Stage && previous.Notes == input.Notes?.Trim() &&
                (previous.Status == ControlStatus.Approved) == input.Approve && previous.Items.Count == input.Items.Count &&
                previous.Items.All(i => input.Items.Any(x => x.Key == i.Key && x.Status == i.Status && x.Notes?.Trim() == i.Notes))
                ? new(ClinicalResultCode.Success, id) : ClinicalOperations.Conflict();
        if (work.Version != input.WorkVersion) return ClinicalOperations.Conflict();
        var template = await ReadTemplateAsync(db, work.Type, input.Stage, ct);
        if (template.Version != input.TemplateVersion) return ClinicalOperations.Conflict();
        if (await db.QualityControls.AnyAsync(x => x.WorkId == id && x.Round == work.Round && x.Stage == input.Stage && x.Status == ControlStatus.Approved, ct) && string.IsNullOrWhiteSpace(input.Notes))
            return ClinicalOperations.Invalid("Una rectificación de un control aprobado requiere un motivo; se conservará el control anterior.");
        var from = work.Status;
        try
        {
            var control = new QualityControl(input.SubmissionId, work, input.Stage, template.Items,
                input.Items.Select(x => new CheckValue(x.Key, x.Status, x.Notes)).ToArray(), input.Approve,
                actor, role, clock.GetUtcNow().UtcDateTime, input.Notes, template.Version);
            work.Apply(control); db.Add(control);
            Event(db, work, from, actor, control.Status == ControlStatus.Approved ? "quality.approved" : "quality.recorded");
            return await ClinicalOperations.SaveAsync(db, id, ct);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return ClinicalOperations.Invalid(ex.Message); }
    }
    public async Task<ClinicalResult> SaveTemplateAsync(OpticalWorkType type, QualityStage stage, Guid version, IReadOnlyList<string> required, CancellationToken ct = default)
    {
        var actor = ClinicalOperations.Demand(current, Permissions.ConfigureQuality);
        IReadOnlyList<CheckDefinition> rules;
        try { rules = QualityCatalog.For(type, stage); }
        catch (ArgumentException ex) { return ClinicalOperations.Invalid(ex.Message); }
        if (required.Distinct().Count() != required.Count || required.Any(k => !rules.Any(x => x.Key == k && x.Applicable)) || rules.Any(x => x.Essential && !required.Contains(x.Key)))
            return ClinicalOperations.Invalid("No se pueden omitir verificaciones esenciales ni exigir ítems no aplicables al tipo de pedido.");
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var template = await db.QualityTemplates.Include(x => x.Items).SingleOrDefaultAsync(x => x.Type == type && x.Stage == stage, ct);
        if ((template?.Version ?? Guid.Empty) != version) return ClinicalOperations.Conflict();
        var effective = rules.Where(r => r.Essential || (template?.Items.SingleOrDefault(i => i.Key == r.Key)?.Required ?? r.Required)).Select(r => r.Key).ToHashSet();
        if (effective.SetEquals(required)) return new(ClinicalResultCode.Success, Guid.Empty);
        if (template is null) { template = new() { Type = type, Stage = stage }; db.Add(template); }
        template.Version = Guid.NewGuid();
        foreach (var rule in rules)
        {
            var item = template.Items.SingleOrDefault(x => x.Key == rule.Key);
            if (item is null) { item = new() { Type = type, Stage = stage, Key = rule.Key }; template.Items.Add(item); }
            item.Required = required.Contains(rule.Key);
        }
        var affected = await db.OpticalWorks.Where(x => x.Type == type && x.Status != WorkStatus.Delivered).ToListAsync(ct);
        foreach (var work in affected) { var from = work.Status; work.Invalidate(); Event(db, work, from, actor, "quality.requirements.changed"); }
        ClinicalOperations.Audit(db, actor, "quality.template.updated", null);
        var result = await ClinicalOperations.SaveAsync(db, Guid.Empty, ct);
        if (result.Succeeded) await transaction.CommitAsync(ct);
        return result;
    }
}
