using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Domain.Clinical;
using OpticaSosa.Domain.Work;
using OpticaSosa.Infrastructure.Clinical;
using Xunit;
namespace OpticaSosa.Web.Tests;
public sealed class ClinicalLinksTests
{
    [Fact]
    public async Task DirectorySeparatesConsultationsAndKeepsPreviousEmitterAndContactAfterClinicalWrite()
    {
        await using var test = await AttachmentTestContext.CreateAsync(); var s = test.Scenario;
        var original = (await s.Clinical.GetAsync(test.CustomerId, test.RecordId))!;
        var first = (await s.Professionals.GetAsync(original.Data.ProfessionalId))!;
        Assert.True((await s.Professionals.UpdateAsync(first.Id, new() { Version = first.Version, FullName = first.FullName, Registration = first.Registration,
            Jurisdiction = first.Jurisdiction, Specialty = "Especialidad ficticia", Phone = "000-000-0101", Email = "profesional@example.invalid" })).Succeeded);
        var other = await s.Professionals.CreateAsync(new() { FullName = "Otro profesional ficticio" }); Assert.True(other.Succeeded);
        var correction = ClinicalScenario.Input(other.Id!.Value); correction.Version = original.Version; correction.CorrectionReason = "Rectificación ficticia de informante";
        Assert.True((await s.Clinical.CorrectAsync(test.CustomerId, test.RecordId, correction)).Succeeded);
        var consultation = new ClinicalInput { Kind = ClinicalKind.VisualHistory, IsFollowUp = true, ProfessionalId = first.Id,
            Source = "Atención ficticia", Notes = "Control informado ficticio", ReportedSymptoms = "Motivo ficticio", ReportedBy = ReportedBy.Professional };
        Assert.True((await s.Clinical.CreateAsync(test.CustomerId, consultation)).Succeeded);
        var directory = new ClinicalDirectory(s.Factory, s.Actor); var overview = await directory.CustomerAsync(test.CustomerId);
        Assert.Equal(1, overview.Recipes); Assert.Equal(1, overview.Histories); Assert.Equal(2, overview.Professionals.Count);
        var historical = await directory.RecordsAsync(professionalId: first.Id); Assert.Equal(2, historical.Items.Count); Assert.Contains(historical.Items, x => x.Id == test.RecordId && x.Revision == 1);
        Assert.Equal("Consulta / control", (await directory.RecordsAsync(customerId: test.CustomerId, histories: true)).Items.Single().Kind);
        Assert.Equal("Especialidad ficticia", (await s.Professionals.GetAsync(first.Id))!.Specialty);
        Assert.Equal("profesional@example.invalid", (await s.Professionals.GetAsync(first.Id))!.Email);
    }
    [Fact]
    public async Task WorkPinsClinicalRevisionAndDatabaseRejectsCrossCustomerLinks()
    {
        await using var test = await AttachmentTestContext.CreateAsync(); var s = test.Scenario; var service = QualityWorkflowTests.Service(s);
        var r = await service.CreateAsync(new() { CustomerId = test.CustomerId, Product = "Trabajo ficticio", Frame = "Armazón ficticio", PrescriptionId = test.RecordId }); Assert.True(r.Succeeded);
        var old = (await s.Clinical.GetAsync(test.CustomerId, test.RecordId))!;
        var correction = ClinicalScenario.Input(old.Data.ProfessionalId); correction.Version = old.Version; correction.CorrectionReason = "Nueva versión ficticia";
        Assert.True((await s.Clinical.CorrectAsync(test.CustomerId, test.RecordId, correction)).Succeeded);
        Assert.Equal(1, (await service.GetAsync(r.Id!.Value))!.PrescriptionRevisionNumber);
        var w = (await service.GetAsync(r.Id.Value))!.Work;
        Assert.True((await service.EditAsync(w.Id, new() { CustomerId = w.CustomerId, Type = w.Type, Version = w.Version, Product = w.Product, Frame = w.Frame, PrescriptionId = test.RecordId })).Succeeded);
        Assert.Equal(2, (await service.GetAsync(w.Id))!.PrescriptionRevisionNumber); Assert.Equal(2, (await service.GetAsync(w.Id))!.Work.Round);
        using var customers = await CustomerScenario.OpenAsync(test.App); var other = (await customers.Customers.CreateAsync(CustomerScenario.Input("FIC-OTRO"))).CustomerId!.Value;
        Assert.False((await service.CreateAsync(new() { CustomerId = other, Product = "Ficticio", Frame = "Ficticio", PrescriptionId = test.RecordId })).Succeeded);
        await using var db = await s.Factory.CreateDbContextAsync();
        db.OpticalWorks.Add(new OpticalWork(Guid.NewGuid(), other, OpticalWorkType.PrescriptionGlasses, "Ficticio", "Ficticio", test.RecordId, s.Actor.Id!, DateTime.UtcNow, old.RevisionId));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
