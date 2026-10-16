using OpticaSosa.Application.Customers;
using OpticaSosa.Domain.Customers;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Customers;

internal static class CustomerQueries
{
    internal static IQueryable<CustomerSummary> Summaries(this IQueryable<Customer> source, OpticaDbContext db) =>
        source.Select(c => new CustomerSummary(c.Id, c.FirstName, c.LastName, c.Document, c.Phone, c.IsActive,
            db.Visits.Any(v => v.CustomerId == c.Id && v.Status == VisitStatus.Completed)));
}
