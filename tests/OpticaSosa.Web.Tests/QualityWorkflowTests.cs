using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Work;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Infrastructure.Work;
using OpticaSosa.Domain.Work;
using Xunit;
namespace OpticaSosa.Web.Tests;
public sealed class QualityWorkflowTests
{
    internal static WorkService Service(ClinicalScenario scenario) => new(scenario.Factory, scenario.Actor, TimeProvider.System);
    internal static async Task<Guid> CreateAsync(SecurityApp app, ClinicalScenario scenario)
    {
        using var customers = await CustomerScenario.OpenAsync(app); var c = (await customers.Customers.CreateAsync(CustomerScenario.Input())).CustomerId!.Value;
        var s = Service(scenario); var result = await s.CreateAsync(new() { CustomerId = c, Product = "Anteojos ficticios", Frame = "Armazón ficticio" });
        Assert.True(result.Succeeded, result.Error); return result.Id!.Value;
    }
    internal static async Task<ControlInput> InputAsync(WorkService s, Guid id, QualityStage stage, bool approve = true)
    {
        var w = (await s.GetAsync(id))!.Work; var t = await s.TemplateAsync(w.Type, stage);
        return new() { Stage = stage, WorkVersion = w.Version, TemplateVersion = t.Version, Approve = approve,
            Items = t.Items.Select(r => new CheckInput { Key = r.Key, Status = r.Applicable ? CheckStatus.Conforming : CheckStatus.NotApplicable, Notes = r.Applicable ? null : "No corresponde al trabajo ficticio" }).ToList() };
    }
    internal static async Task PrepareAsync(WorkService s, Guid id)
    {
        foreach (var command in new[] { "prepare", "control" }) Assert.True((await s.TransitionAsync(id, (await s.GetAsync(id))!.Work.Version, command)).Succeeded);
    }
    [Fact]
    public async Task EndToEndPersistsRolesDatesChecksSeparateOrdersAndAudit()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync(); using var pro = await ClinicalScenario.OpenAsync(app); using var sales = await ClinicalScenario.OpenAsync(app, "ventas.ficticio");
        var s = Service(pro); var salesService = Service(sales); var id = await CreateAsync(app, pro); var w = (await s.GetAsync(id))!.Work;
        var other = await s.CreateAsync(new() { CustomerId = w.CustomerId, Product = "Otro trabajo ficticio", Frame = "Otro armazón ficticio" });
        Assert.False((await s.TransitionAsync(id, w.Version, "deliver")).Succeeded); await PrepareAsync(s, id);
        Assert.False((await s.TransitionAsync(id, (await s.GetAsync(id))!.Work.Version, "ready")).Succeeded);
        var before = DateTime.UtcNow; var technical = await InputAsync(s, id, QualityStage.Technical);
        Assert.True((await s.SaveControlAsync(id, technical)).Succeeded);
        Assert.Equal(WorkStatus.PendingControl, (await s.GetAsync(id))!.Work.Status);
        Assert.Empty((await salesService.GetAsync(id))!.Controls);
        Assert.True((await salesService.SaveControlAsync(id, await InputAsync(salesService, id, QualityStage.Delivery))).Succeeded);
        var details = (await s.GetAsync(id))!; Assert.Equal(WorkStatus.ReadyForDelivery, details.Work.Status); Assert.Equal(2, details.ControlCount);
        Assert.All(details.Controls, c => { Assert.InRange(c.AtUtc, before, DateTime.UtcNow); Assert.Equal(c.AtUtc, c.ApprovedAtUtc); Assert.NotEmpty(c.ActorId); Assert.All(c.Items, i => Assert.Equal(CheckStatus.Conforming, i.Status)); });
        Assert.Equal(ResponsibleRole.Sales, details.Controls.Single(c => c.Stage == QualityStage.Delivery).Role);
        Assert.Empty((await s.GetAsync(other.Id!.Value))!.Controls);
        Assert.True((await salesService.TransitionAsync(id, details.Work.Version, "deliver")).Succeeded);
        Assert.Equal(WorkStatus.Delivered, (await s.GetAsync(id))!.Work.Status);
        Assert.Equal(2, await pro.Db.AuditEntries.CountAsync(x => x.Action == "quality.approved"));
    }
    [Theory] [InlineData(CheckStatus.Pending)] [InlineData(CheckStatus.Observed)] [InlineData(CheckStatus.NotApplicable)]
    public async Task DirectServiceRejectsInvalidApprovalWithoutWritingControlOrState(CheckStatus status)
    {
        await using var app = new SecurityApp(); await app.InitializeAsync(); using var pro = await ClinicalScenario.OpenAsync(app);
        var s = Service(pro); var id = await CreateAsync(app, pro); await PrepareAsync(s, id); var input = await InputAsync(s, id, QualityStage.Technical);
        input.Items[0].Status = status; input.Items[0].Notes = "Motivo ficticio";
        Assert.False((await s.SaveControlAsync(id, input)).Succeeded); Assert.Empty((await s.GetAsync(id))!.Controls); Assert.Equal(WorkStatus.PendingControl, (await s.GetAsync(id))!.Work.Status);
    }
    [Fact]
    public async Task ForbiddenRolesCannotReadClinicalDirectoryOrApproveTechnicalEvenCallingService()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync(); using var pro = await ClinicalScenario.OpenAsync(app); var s = Service(pro); var id = await CreateAsync(app, pro); await PrepareAsync(s, id);
        var input = await InputAsync(s, id, QualityStage.Technical);
        foreach (var user in new[] { "ventas.ficticio", "admin.ficticio" }) {
            using var denied = await ClinicalScenario.OpenAsync(app, user); var service = Service(denied);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SaveControlAsync(id, input));
            var directory = new OpticaSosa.Infrastructure.Clinical.ClinicalDirectory(denied.Factory, denied.Actor);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => directory.RecordsAsync());
        }
    }
    [Fact]
    public async Task RevisionsAndObservedCorrectionPreserveOldControlsAndInvalidateApprovals()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync(); using var pro = await ClinicalScenario.OpenAsync(app); var s = Service(pro);
        var id = await CreateAsync(app, pro); await PrepareAsync(s, id);
        foreach (var stage in Enum.GetValues<QualityStage>()) Assert.True((await s.SaveControlAsync(id, await InputAsync(s, id, stage))).Succeeded);
        var old = (await s.GetAsync(id))!; Assert.True((await s.EditAsync(id, new() { CustomerId = old.Work.CustomerId, Version = old.Work.Version, Type = old.Work.Type, Product = "Cambio ficticio", Frame = old.Work.Frame })).Succeeded);
        var changed = (await s.GetAsync(id))!; Assert.False(changed.Work.TechnicalApproved); Assert.False(changed.Work.DeliveryApproved); Assert.Equal(2, changed.ControlCount);
        Assert.False((await s.SaveControlAsync(id, await InputAsync(s, id, QualityStage.Technical))).Succeeded);
        Assert.True((await s.TransitionAsync(id, changed.Work.Version, "control")).Succeeded);
        var observed = await InputAsync(s, id, QualityStage.Technical, false); observed.Items[0].Status = CheckStatus.Observed;
        Assert.True((await s.SaveControlAsync(id, observed)).Succeeded); Assert.Equal(WorkStatus.Observed, (await s.GetAsync(id))!.Work.Status);
        Assert.False((await s.SaveControlAsync(id, await InputAsync(s, id, QualityStage.Technical))).Succeeded);
        Assert.True((await s.TransitionAsync(id, (await s.GetAsync(id))!.Work.Version, "correct")).Succeeded);
        Assert.Equal(3, (await s.GetAsync(id))!.Work.Round); Assert.Equal(3, (await s.GetAsync(id))!.ControlCount);
    }
    [Fact]
    public async Task ConcurrentApprovalsAndResubmissionDoNotDuplicateOrLoseChecks()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync(); using var pro = await ClinicalScenario.OpenAsync(app); var s = Service(pro); var id = await CreateAsync(app, pro); await PrepareAsync(s, id);
        var a = await InputAsync(s, id, QualityStage.Technical); var b = await InputAsync(s, id, QualityStage.Delivery);
        var results = await Task.WhenAll(s.SaveControlAsync(id, a), s.SaveControlAsync(id, b));
        Assert.Single(results, r => r.Succeeded); Assert.Single((await s.GetAsync(id))!.Controls);
        var winner = results[0].Succeeded ? a : b; Assert.True((await s.SaveControlAsync(id, winner)).Succeeded); Assert.Single((await s.GetAsync(id))!.Controls);
        Assert.Equal(WorkStatus.PendingControl, (await s.GetAsync(id))!.Work.Status);
    }
    [Fact]
    public async Task AdminRequirementsInvalidatePendingOrdersButPreserveHistoryAndRejectStaleApproval()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync(); using var pro = await ClinicalScenario.OpenAsync(app); using var admin = await ClinicalScenario.OpenAsync(app, "admin.ficticio");
        var s = Service(pro); var adminService = Service(admin); var id = await CreateAsync(app, pro); await PrepareAsync(s, id);
        Assert.True((await s.SaveControlAsync(id, await InputAsync(s, id, QualityStage.Technical))).Succeeded);
        var stale = await InputAsync(s, id, QualityStage.Delivery); var template = await adminService.TemplateAsync(OpticalWorkType.PrescriptionGlasses, QualityStage.Delivery);
        Assert.False((await adminService.SaveTemplateAsync(template.Type, template.Stage, template.Version, [])).Succeeded);
        Assert.True((await adminService.SaveTemplateAsync(template.Type, template.Stage, template.Version, template.Items.Where(x => x.Essential).Select(x => x.Key).ToArray())).Succeeded);
        Assert.False((await s.SaveControlAsync(id, stale)).Succeeded); var work = (await s.GetAsync(id))!; Assert.Equal(2, work.Work.Round); Assert.False(work.Work.TechnicalApproved); Assert.Single(work.Controls);
    }
}
