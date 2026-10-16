using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Domain.Clinical;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class ClinicalWorkflowTests
{
    [Fact]
    public async Task CorrectionsKeepPriorValuesSnapshotsAndAuditWhileRejectingStaleWrites()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var customers = await CustomerScenario.OpenAsync(app);
        var customer = (await customers.Customers.CreateAsync(CustomerScenario.Input())).CustomerId!.Value;
        using var scenario = await ClinicalScenario.OpenAsync(app);
        var professional = await scenario.AddProfessionalAsync();
        var input = ClinicalScenario.Input(professional);
        var created = await scenario.Clinical.CreateAsync(customer, input);
        Assert.True(created.Succeeded);
        Assert.True((await scenario.Clinical.CreateAsync(customer, input)).Succeeded);
        var original = (await scenario.Clinical.GetAsync(customer, created.Id!.Value))!;
        input.SubmissionId = Guid.NewGuid(); input.Version = original.Version;
        input.CorrectionReason = "Corrección de transcripción ficticia"; input.Right.Sphere = 1.375m;
        Assert.True((await scenario.Clinical.CorrectAsync(customer, original.Id, input)).Succeeded);
        Assert.True((await scenario.Clinical.CorrectAsync(customer, original.Id, input)).Succeeded);
        input.SubmissionId = Guid.NewGuid();
        Assert.Equal(ClinicalResultCode.Conflict, (await scenario.Clinical.CorrectAsync(customer, original.Id, input)).Code);
        var historical = (await scenario.Clinical.GetAsync(customer, original.Id, 1))!;
        var latest = (await scenario.Clinical.GetAsync(customer, original.Id))!;
        Assert.Equal(1.125m, historical.Data.Right.Sphere); Assert.Equal(1.375m, latest.Data.Right.Sphere);
        Assert.Equal(-2.375m, latest.Data.Left.Sphere); Assert.Equal(0m, latest.Data.Right.Cylinder);
        Assert.Null(latest.Data.Left.Cylinder); Assert.Equal(62.25m, latest.Data.PupillaryDistance);
        Assert.Equal(2, latest.History.Count);
        Assert.Single((await scenario.Clinical.ListAsync(customer)).Items);
        Assert.Equal(2, await scenario.Db.ClinicalRevisions.CountAsync());
        var audit = await scenario.Db.AuditEntries.Where(x => x.Action.StartsWith("clinical.")).ToListAsync();
        Assert.Single(audit, x => x.Action == "clinical.created");
        Assert.Single(audit, x => x.Action == "clinical.corrected");
        Assert.All(audit, x => { Assert.DoesNotContain("ficticia", x.Action); Assert.DoesNotContain("ficticia", x.TargetId ?? ""); });
    }

    [Fact]
    public async Task CrossCustomerVisitsAndRecordsAreRejectedAndInvalidWritesAreAtomic()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var customers = await CustomerScenario.OpenAsync(app);
        var first = (await customers.Customers.CreateAsync(CustomerScenario.Input("FIC-A"))).CustomerId!.Value;
        var second = (await customers.Customers.CreateAsync(CustomerScenario.Input("FIC-B"))).CustomerId!.Value;
        var customer = (await customers.Customers.GetAsync(first))!;
        await customers.Visits.StartAsync(first, new() { Reason = "Visita ficticia", CustomerVersion = customer.Version });
        var visit = (await customers.Customers.GetAsync(first))!.Visits.Single().Id;
        using var scenario = await ClinicalScenario.OpenAsync(app);
        var professional = await scenario.AddProfessionalAsync();
        var input = ClinicalScenario.Input(professional); input.VisitId = visit;
        Assert.False((await scenario.Clinical.CreateAsync(second, input)).Succeeded);
        Assert.True((await scenario.Clinical.CreateAsync(first, input)).Succeeded);
        Assert.Null(await scenario.Clinical.GetAsync(second, input.SubmissionId));
        Assert.Equal(ClinicalResultCode.NotFound, (await scenario.Clinical.CorrectAsync(second, input.SubmissionId, input)).Code);
        var invalid = ClinicalScenario.Input(professional); invalid.Right.Sphere = 1.1234m;
        Assert.False((await scenario.Clinical.CreateAsync(first, invalid)).Succeeded);
        invalid = ClinicalScenario.Input(professional); invalid.PupillaryDistance = 62.125m;
        Assert.False((await scenario.Clinical.CreateAsync(first, invalid)).Succeeded);
        Assert.Equal(1, await scenario.Db.ClinicalRecords.CountAsync());
        Assert.Equal(1, await scenario.Db.ClinicalRevisions.CountAsync());
        Assert.Equal(1, await scenario.Db.AuditEntries.CountAsync(x => x.Action == "clinical.created"));
    }

    [Fact]
    public async Task VisualHistoryRemainsSeparateAndInactiveProfessionalsCannotSignNewRecords()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var customers = await CustomerScenario.OpenAsync(app);
        var customer = (await customers.Customers.CreateAsync(CustomerScenario.Input())).CustomerId!.Value;
        using var scenario = await ClinicalScenario.OpenAsync(app);
        var professional = await scenario.AddProfessionalAsync();
        var input = new ClinicalInput { Kind = ClinicalKind.VisualHistory, ProfessionalId = professional, Source = "Informe ficticio", Notes = "Antecedente registrado por profesional ficticio" };
        Assert.True((await scenario.Clinical.CreateAsync(customer, input)).Succeeded);
        Assert.Empty(await scenario.Db.EyeMeasurements.ToListAsync());
        var details = (await scenario.Professionals.GetAsync(professional))!;
        Assert.True((await scenario.Professionals.UpdateAsync(professional, new() { Version = details.Version, FullName = "Nombre nuevo ficticio", IsActive = false })).Succeeded);
        Assert.False((await scenario.Clinical.CreateAsync(customer, ClinicalScenario.Input(professional))).Succeeded);
        Assert.Equal("Profesional ficticio", (await scenario.Clinical.GetAsync(customer, input.SubmissionId))!.ProfessionalName);
    }
}
