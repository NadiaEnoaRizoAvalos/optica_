using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Customers;
using OpticaSosa.Domain.Customers;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class VisitWorkflowTests
{
    [Fact]
    public async Task OnlyEarlierCompletedVisitsMakeANewVisitRecurrentAndHistoryNeverChanges()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var s = await CustomerScenario.OpenAsync(app);
        var customer = CustomerScenario.Input(); await s.Customers.CreateAsync(customer);
        async Task<VisitSummary> Start()
        {
            s.Clock.Advance();
            var details = (await s.Customers.GetAsync(customer.SubmissionId))!;
            var command = new VisitInput { Reason = "Consulta comercial ficticia", CustomerVersion = details.Version };
            Assert.True((await s.Visits.StartAsync(customer.SubmissionId, command)).Succeeded);
            Assert.True((await s.Visits.StartAsync(customer.SubmissionId, command)).Succeeded);
            return (await s.Customers.GetAsync(customer.SubmissionId))!.Visits.Single(v => v.Id == command.SubmissionId);
        }
        var cancelled = await Start(); s.Clock.Advance();
        Assert.True((await s.Visits.CloseAsync(customer.SubmissionId, cancelled.Id, cancelled.Version, false)).Succeeded);
        Assert.False((await s.Customers.GetAsync(customer.SubmissionId))!.Customer.IsRecurrent);
        var first = await Start(); Assert.False(first.WasRecurrent); s.Clock.Advance();
        Assert.True((await s.Visits.CloseAsync(customer.SubmissionId, first.Id, first.Version, true)).Succeeded);
        Assert.True((await s.Visits.CloseAsync(customer.SubmissionId, first.Id, first.Version, true)).Succeeded);
        Assert.True((await s.Customers.GetAsync(customer.SubmissionId))!.Customer.IsRecurrent);
        var next = await Start(); Assert.True(next.WasRecurrent);
        var history = (await s.Customers.GetAsync(customer.SubmissionId))!.Visits;
        Assert.Equal(3, history.Count);
        Assert.False(history.Single(v => v.Id == first.Id).WasRecurrent);
        Assert.Equal(1, await s.Db.AuditEntries.CountAsync(x => x.Action == "visit.completed"));
    }

    [Fact]
    public async Task VisitsCannotCrossClientsAndDeactivationPreservesHistoryAndRejectsNewVisits()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var s = await CustomerScenario.OpenAsync(app);
        var input = CustomerScenario.Input(); await s.Customers.CreateAsync(input);
        var other = CustomerScenario.Input("FIC-002"); await s.Customers.CreateAsync(other);
        var details = (await s.Customers.GetAsync(input.SubmissionId))!;
        var start = new VisitInput { Reason = "Retiro ficticio", CustomerVersion = details.Version };
        Assert.True((await s.Visits.StartAsync(input.SubmissionId, start)).Succeeded);
        details = (await s.Customers.GetAsync(input.SubmissionId))!;
        Assert.False((await s.Customers.DeactivateAsync(input.SubmissionId, details.Version)).Succeeded);
        var visit = details.Visits.Single();
        Assert.Equal(CustomerResultCode.NotFound, (await s.Visits.CloseAsync(other.SubmissionId, visit.Id, visit.Version, true)).Code);
        s.Clock.Advance(); await s.Visits.CloseAsync(input.SubmissionId, visit.Id, visit.Version, true);
        Assert.True((await s.Customers.DeactivateAsync(input.SubmissionId, details.Version)).Succeeded);
        Assert.Single((await s.Customers.GetAsync(input.SubmissionId))!.Visits);
        Assert.False((await s.Visits.StartAsync(input.SubmissionId, new() { Reason = "No permitida", CustomerVersion = details.Version })).Succeeded);
        var entity = await s.Db.Customers.FindAsync(input.SubmissionId);
        s.Db.Customers.Remove(entity!);
        await Assert.ThrowsAsync<DbUpdateException>(() => s.Db.SaveChangesAsync());
    }

    [Theory]
    [InlineData("ventas.ficticio")]
    [InlineData("profesional.ficticio")]
    public async Task StaffCanManageAdministrativeAttentionButOnlyAdminCanDeactivateCustomers(string user)
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var s = await CustomerScenario.OpenAsync(app, user);
        var input = CustomerScenario.Input(); Assert.True((await s.Customers.CreateAsync(input)).Succeeded);
        var details = (await s.Customers.GetAsync(input.SubmissionId))!;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => s.Customers.DeactivateAsync(input.SubmissionId, details.Version));
    }
}
