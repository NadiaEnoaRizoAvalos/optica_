using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpticaSosa.Application.Customers;
using OpticaSosa.Application.Security;
using OpticaSosa.Infrastructure.Customers;
using OpticaSosa.Infrastructure.Persistence;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class CustomerBoundaryTests
{
    [Fact]
    public async Task ApplicationRejectsUnauthenticatedCallsBeforeReadingOrWriting()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var scope = app.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<OpticaDbContext>>();
        var customers = new CustomerService(factory, new Anonymous(), TimeProvider.System);
        var visits = new VisitService(factory, new Anonymous(), TimeProvider.System);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => customers.SearchAsync(null));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => customers.GetAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => customers.CreateAsync(CustomerScenario.Input()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => customers.UpdateAsync(Guid.NewGuid(), CustomerScenario.Input()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => customers.DeactivateAsync(Guid.NewGuid(), Guid.NewGuid()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => visits.StartAsync(Guid.NewGuid(), new VisitInput()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => visits.CloseAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), true));
    }

    [Fact]
    public async Task HistoryPaginationPreservesAllVisitsWithoutOverlap()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var s = await CustomerScenario.OpenAsync(app);
        var input = CustomerScenario.Input(); await s.Customers.CreateAsync(input);
        for (var i = 0; i < 26; i++)
        {
            s.Clock.Advance();
            var current = (await s.Customers.GetAsync(input.SubmissionId))!;
            var visit = new VisitInput { CustomerVersion = current.Version, Reason = "Atención comercial ficticia" };
            Assert.True((await s.Visits.StartAsync(input.SubmissionId, visit)).Succeeded);
            var version = await s.Db.Visits.AsNoTracking().Where(v => v.Id == visit.SubmissionId).Select(v => v.Version).SingleAsync();
            s.Clock.Advance();
            Assert.True((await s.Visits.CloseAsync(input.SubmissionId, visit.SubmissionId, version, true)).Succeeded);
        }
        var first = (await s.Customers.GetAsync(input.SubmissionId))!;
        var second = (await s.Customers.GetAsync(input.SubmissionId, 2))!;
        Assert.Equal(25, first.Visits.Count); Assert.True(first.HasOlderVisits);
        Assert.Single(second.Visits); Assert.False(second.HasOlderVisits);
        Assert.Empty(first.Visits.Select(v => v.Id).Intersect(second.Visits.Select(v => v.Id)));
        Assert.False(second.Visits.Single().WasRecurrent);
    }

    private sealed class Anonymous : ICurrentUser
    {
        public string? Id => null;
        public bool HasPermission(string permission) => false;
    }
}
