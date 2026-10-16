using Microsoft.EntityFrameworkCore;
using OpticaSosa.Domain.Clinical;
using OpticaSosa.Domain.Customers;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class LongClinicalHistoryTests
{
    [Fact]
    public async Task YearsOfRecipesAndVisitsCanBeTraversedWithoutTruncationOrReplacingPriorDocuments()
    {
        await using var test = await AttachmentTestContext.CreateAsync();
        var original = (await test.Scenario.Clinical.GetAsync(test.CustomerId, test.RecordId))!;
        var professional = await test.Scenario.Db.Professionals.SingleAsync();
        var date = new DateOnly(2000, 1, 1);
        for (int n = 0; n < 103; n++)
        {
            var record = new ClinicalRecord(Guid.NewGuid(), test.CustomerId, null,
                n % 2 == 0 ? ClinicalKind.MedicalPrescription : ClinicalKind.OpticalPrescription);
            var data = original.Data with { Date = date.AddMonths(n), Source = "Fuente histórica ficticia " + n };
            var revision = new ClinicalRevision(Guid.NewGuid(), record, data, professional, test.Scenario.Actor.Id!, DateTime.UtcNow, null);
            record.AddRevision(); test.Scenario.Db.Add(record); test.Scenario.Db.Add(revision);
            var started = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(n);
            var visit = new Visit(Guid.NewGuid(), test.CustomerId, "Control administrativo ficticio", null, test.Scenario.Actor.Id!, started, n > 0);
            visit.Close(true, test.Scenario.Actor.Id!, started.AddHours(1)); test.Scenario.Db.Add(visit);
        }
        await test.Scenario.Db.SaveChangesAsync();
        var seen = new List<OpticaSosa.Application.Clinical.ClinicalSummary>();
        for (int page = 1; ; page++)
        {
            var history = await test.Scenario.Clinical.ListAsync(test.CustomerId, page);
            seen.AddRange(history.Items); if (!history.HasMore) break;
        }
        Assert.Equal(104, seen.Count); Assert.Equal(104, seen.Select(x => x.Id).Distinct().Count());
        Assert.Equal(seen.OrderByDescending(x => x.Date).Select(x => x.Date), seen.Select(x => x.Date));
        Assert.Contains(seen, x => x.Date.Year == 2000);
        var directory = new OpticaSosa.Infrastructure.Clinical.ClinicalDirectory(test.Scenario.Factory, test.Scenario.Actor);
        var directoryIds = new List<Guid>();
        for (int page = 1; ; page++) { var rows = await directory.RecordsAsync(customerId: test.CustomerId, histories: false, page: page);
            directoryIds.AddRange(rows.Items.Select(x => x.Id)); if (!rows.HasMore) break; }
        Assert.Equal(104, directoryIds.Distinct().Count());
        Assert.Equal(original.Data, (await test.Scenario.Clinical.GetAsync(test.CustomerId, test.RecordId))!.Data);
        using var customers = await CustomerScenario.OpenAsync(test.App);
        var visits = new List<Guid>();
        for (int page = 1; ; page++)
        {
            var details = (await customers.Customers.GetAsync(test.CustomerId, page))!;
            visits.AddRange(details.Visits.Select(x => x.Id)); if (!details.HasOlderVisits) break;
        }
        Assert.Equal(103, visits.Distinct().Count());
        Assert.Equal(10001, (await customers.Customers.GetAsync(test.CustomerId, 10001))!.HistoryPage);
        Assert.Equal(100001, (await test.Scenario.Clinical.ListAsync(test.CustomerId, 100001)).Page);
    }

    [Fact]
    public async Task ManyCorrectionsPreserveAllVersionsAndDocumentsRemainAttachedToTheirOriginalRevision()
    {
        await using var test = await AttachmentTestContext.CreateAsync();
        var saved = await test.UploadAsync(); Assert.True(saved.Succeeded);
        var first = (await test.Scenario.Clinical.GetAsync(test.CustomerId, test.RecordId))!;
        for (int n = 0; n < 30; n++)
        {
            var latest = (await test.Scenario.Clinical.GetAsync(test.CustomerId, test.RecordId))!;
            var input = ClinicalScenario.Input(latest.Data.ProfessionalId);
            input.Version = latest.Version; input.CorrectionReason = "Corrección ficticia " + n;
            input.Right.Sphere = n / 1000m;
            Assert.True((await test.Scenario.Clinical.CorrectAsync(test.CustomerId, test.RecordId, input)).Succeeded);
        }
        var current = (await test.Scenario.Clinical.GetAsync(test.CustomerId, test.RecordId))!;
        Assert.Equal(31, current.LatestNumber); Assert.Equal(25, current.History.Count); Assert.True(current.HasOlderRevisions);
        Assert.Equal(6, (await test.Scenario.Clinical.GetAsync(test.CustomerId, test.RecordId, historyPage: 2))!.History.Count);
        Assert.Empty((await test.Scenario.Attachments.ListAsync(test.CustomerId, test.RecordId, current.RevisionId)).Items);
        Assert.Single((await test.Scenario.Attachments.ListAsync(test.CustomerId, test.RecordId, first.RevisionId)).Items);
        Assert.Equal(BusinessForms.Logo(), await test.DownloadAsync(saved.Id!.Value));
        Assert.Equal(first.Data, (await test.Scenario.Clinical.GetAsync(test.CustomerId, test.RecordId, 1))!.Data);
    }
}
