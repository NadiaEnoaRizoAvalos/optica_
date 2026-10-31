using System.Net;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpticaSosa.Domain.Work;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class CommercialQualityTests
{
    [Theory]
    [InlineData(OpticalWorkType.ContactLenses, true)]
    [InlineData(OpticalWorkType.CommercialGlasses, true)]
    [InlineData(OpticalWorkType.OpticalRepair, true)]
    [InlineData(OpticalWorkType.CommercialFrame, false)]
    [InlineData(OpticalWorkType.Accessory, false)]
    [InlineData(OpticalWorkType.MechanicalRepair, false)]
    public async Task MatrixUsesExistingEngineWithExplicitClinicalPermissionsAndTwoStages(OpticalWorkType type, bool professionalRequired)
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var customers = await CustomerScenario.OpenAsync(app);
        var customer = (await customers.Customers.CreateAsync(CustomerScenario.Input())).CustomerId!.Value;
        using var pro = await ClinicalScenario.OpenAsync(app);
        using var sales = await ClinicalScenario.OpenAsync(app, "ventas.ficticio");
        var professional = QualityWorkflowTests.Service(pro); var commercial = QualityWorkflowTests.Service(sales);
        var result = await professional.CreateAsync(new() { CustomerId = customer, Type = type,
            Product = "Producto ficticio para calidad", Frame = QualityCatalog.RequiresFrame(type) ? "Armazón ficticio" : "" });
        Assert.True(result.Succeeded, result.Error); var id = result.Id!.Value;
        await QualityWorkflowTests.PrepareAsync(professional, id);
        var input = await QualityWorkflowTests.InputAsync(commercial, id, QualityStage.Technical);
        Assert.All((await commercial.TemplateAsync(type, QualityStage.Technical)).Items, x => Assert.True(x.Essential && x.Applicable && x.Required));
        if (professionalRequired)
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => commercial.SaveControlAsync(id, input));
            Assert.True((await professional.SaveControlAsync(id, input)).Succeeded);
        }
        else Assert.True((await commercial.SaveControlAsync(id, input)).Succeeded);
        Assert.Equal(WorkStatus.PendingControl, (await professional.GetAsync(id))!.Work.Status);
        Assert.False((await commercial.TransitionAsync(id, (await commercial.GetAsync(id))!.Work.Version, "deliver")).Succeeded);
        Assert.True((await commercial.SaveControlAsync(id, await QualityWorkflowTests.InputAsync(commercial, id, QualityStage.Delivery))).Succeeded);
        Assert.True((await commercial.TransitionAsync(id, (await commercial.GetAsync(id))!.Work.Version, "deliver")).Succeeded);
        var details = (await professional.GetAsync(id))!; Assert.Equal(2, details.ControlCount);
        Assert.Equal(WorkStatus.Delivered, details.Work.Status);
        Assert.Equal(professionalRequired ? 1 : 2, (await commercial.GetAsync(id))!.ControlCount);
        await using var db = await pro.Factory.CreateDbContextAsync();
        if (professionalRequired)
            await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE QualityControls SET Role = 1 WHERE WorkId = {id} AND Stage = 0"));
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE QualityControls SET WorkType = 99 WHERE WorkId = {id}"));
    }

    [Fact]
    public async Task SalesCanUseNonClinicalTechnicalScreenButAdminAloneCannotApprove()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var customers = await CustomerScenario.OpenAsync(app);
        var customer = (await customers.Customers.CreateAsync(CustomerScenario.Input())).CustomerId!.Value;
        using var pro = await ClinicalScenario.OpenAsync(app);
        using var admin = await ClinicalScenario.OpenAsync(app, "admin.ficticio");
        var service = QualityWorkflowTests.Service(pro);
        var created = await service.CreateAsync(new() { CustomerId = customer, Type = OpticalWorkType.Accessory, Product = "Accesorio ficticio" });
        Assert.True(created.Succeeded, created.Error); var id = created.Id!.Value;
        await QualityWorkflowTests.PrepareAsync(service, id);
        using var browser = app.Browser(); await BrowserForms.LoginAsync(browser, "ventas.ficticio");
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync($"/pedidos/{id}/control?etapa=0")).StatusCode);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await QualityWorkflowTests.Service(admin).SaveControlAsync(id,
            await QualityWorkflowTests.InputAsync(service, id, QualityStage.Technical)));
    }

    [Fact]
    public async Task EssentialCommercialChecksCannotBeRemovedOrMarkedNotApplicable()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var pro = await ClinicalScenario.OpenAsync(app);
        using var admin = await ClinicalScenario.OpenAsync(app, "admin.ficticio");
        var service = QualityWorkflowTests.Service(pro); var management = QualityWorkflowTests.Service(admin);
        var template = await management.TemplateAsync(OpticalWorkType.ContactLenses, QualityStage.Technical);
        Assert.False((await management.SaveTemplateAsync(template.Type, template.Stage, template.Version, template.Items.Skip(1).Select(x => x.Key).ToArray())).Succeeded);
        var work = new OpticalWork(Guid.NewGuid(), Guid.NewGuid(), OpticalWorkType.ContactLenses, "Contacto ficticio", "", null, "ficticio", DateTime.UtcNow);
        var rules = QualityCatalog.For(work.Type, QualityStage.Technical);
        var values = rules.Select(x => new CheckValue(x.Key, CheckStatus.Conforming)).ToArray();
        values[0] = values[0] with { Status = CheckStatus.NotApplicable, Notes = "No debe admitirse" };
        Assert.Throws<ArgumentException>(() => new QualityControl(Guid.NewGuid(), work, QualityStage.Technical, rules, values,
            true, "ficticio", ResponsibleRole.Professional, DateTime.UtcNow, null, Guid.Empty));
        Assert.Throws<ArgumentException>(() => new QualityControl(Guid.NewGuid(), work, QualityStage.Technical, rules.Skip(1).ToArray(), values.Skip(1).ToArray(),
            true, "ficticio", ResponsibleRole.Professional, DateTime.UtcNow, null, Guid.Empty));
    }
}
