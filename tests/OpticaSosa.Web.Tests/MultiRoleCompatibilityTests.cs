using System.Text.Json;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Work;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class MultiRoleCompatibilityTests
{
    [Fact]
    public async Task RoleChangesPreserveClinicalVersionsAndHistoricalQualityResponsibilities()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var pro = await ClinicalScenario.OpenAsync(app);
        var work = QualityWorkflowTests.Service(pro);
        var id = await QualityWorkflowTests.CreateAsync(app, pro);
        var customer = (await work.GetAsync(id))!.Work.CustomerId;
        var professional = await pro.AddProfessionalAsync();
        var input = ClinicalScenario.Input(professional);
        var record = await pro.Clinical.CreateAsync(customer, input); Assert.True(record.Succeeded);
        var before = await pro.Clinical.GetAsync(customer, record.Id!.Value);
        input.Version = before!.Version; input.SubmissionId = Guid.NewGuid();
        input.CorrectionReason = "Corrección ficticia"; input.Right.Sphere = 1.375m;
        Assert.True((await pro.Clinical.CorrectAsync(customer, record.Id.Value, input)).Succeeded);
        await QualityWorkflowTests.PrepareAsync(work, id);
        Assert.True((await work.SaveControlAsync(id, await QualityWorkflowTests.InputAsync(work, id, QualityStage.Technical))).Succeeded);
        using var sales = await ClinicalScenario.OpenAsync(app, "ventas.ficticio");
        var delivery = QualityWorkflowTests.Service(sales);
        Assert.True((await delivery.SaveControlAsync(id, await QualityWorkflowTests.InputAsync(delivery, id, QualityStage.Delivery))).Succeeded);
        var controlsBefore = JsonSerializer.Serialize((await work.GetAsync(id))!.Controls);
        var clinicalBefore = JsonSerializer.Serialize(await pro.Clinical.GetAsync(customer, record.Id.Value));
        using var accounts = await AccountScenario.OpenAsync(app);
        Assert.True((await accounts.SetRolesAsync("profesional.ficticio", Roles.Administrator, Roles.Professional, Roles.Sales)).Succeeded);
        Assert.True((await accounts.SetRolesAsync("ventas.ficticio", Roles.Professional, Roles.Sales)).Succeeded);
        using var renewed = await ClinicalScenario.OpenAsync(app);
        Assert.Equal(clinicalBefore, JsonSerializer.Serialize(await renewed.Clinical.GetAsync(customer, record.Id.Value)));
        var controlsAfter = (await QualityWorkflowTests.Service(renewed).GetAsync(id))!.Controls;
        Assert.Equal(controlsBefore, JsonSerializer.Serialize(controlsAfter));
        Assert.Equal(ResponsibleRole.Sales, controlsAfter.Single(x => x.Stage == QualityStage.Delivery).Role);
        Assert.Equal(ResponsibleRole.Professional, controlsAfter.Single(x => x.Stage == QualityStage.Technical).Role);
    }
}
