using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Customers;
using OpticaSosa.Application.Security;
using OpticaSosa.Application.Clinical;
using OpticaSosa.Domain.Customers;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Customers;

public sealed partial class CustomerService(IDbContextFactory<OpticaDbContext> factory, ICurrentUser current, TimeProvider clock) : ICustomerService
{
    public async Task<CustomerSearchResult> SearchAsync(string? query, bool includeInactive = false, CancellationToken ct = default)
    {
        var actor = CustomerOperations.Demand(current, Permissions.ReadCustomers);
        if ((query?.Length ?? 0) > 100) throw new ArgumentException("La búsqueda admite hasta 100 caracteres.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = db.Customers.AsNoTracking().Where(c => includeInactive || c.IsActive);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var folded = CustomerData.Fold(query.Trim());
            var byName = rows;
            foreach (var word in folded.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                byName = byName.Where(c => c.SearchName.Contains(word));
            var nameIds = byName.Select(c => c.Id);
            var document = CustomerData.DocumentKey(query);
            var phone = CustomerData.PhoneKey(query);
            rows = rows.Where(c => nameIds.Contains(c.Id) ||
                (document.Length > 0 && c.DocumentKey.Contains(document)) ||
                (phone.Length > 0 && c.PhoneKey != null && c.PhoneKey.Contains(phone)) ||
                (c.Email != null && c.Email.ToUpper().Contains(folded)));
        }
        var result = await rows.OrderBy(c => c.LastName).ThenBy(c => c.FirstName).ThenBy(c => c.Id)
            .Summaries(db).Take(101).ToListAsync(ct);
        CustomerOperations.Audit(db, actor, "customers.searched", null);
        await db.SaveChangesAsync(ct);
        return new(result.Take(100).ToList(), result.Count > 100);
    }

    public async Task<CustomerDetails?> GetAsync(Guid customerId, int historyPage = 1, CancellationToken ct = default)
    {
        var actor = CustomerOperations.Demand(current, Permissions.ReadCustomers);
        await using var db = await factory.CreateDbContextAsync(ct);
        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == customerId, ct);
        if (customer is null) return null;
        var summary = await db.Customers.Where(c => c.Id == customerId).Summaries(db).SingleAsync(ct);
        var page = Math.Max(historyPage, 1);
        if (!HistoryPaging.IsValid(page)) throw new ArgumentException("Página fuera de la capacidad técnica de consulta.");
        var history = await db.Visits.AsNoTracking().Where(v => v.CustomerId == customerId)
            .OrderByDescending(v => v.StartedAtUtc).ThenByDescending(v => v.Id).Skip(HistoryPaging.Offset(page)).Take(HistoryPaging.Size + 1)
            .Select(v => new VisitSummary(v.Id, v.Reason, v.AdministrativeNotes, v.StartedAtUtc,
                v.EndedAtUtc, v.Status, v.WasRecurrent, v.Version, v.RecordedByUserId, v.EndedByUserId)).ToListAsync(ct);
        CustomerOperations.Audit(db, actor, "customer.viewed", customerId);
        await db.SaveChangesAsync(ct);
        var count = await db.Visits.CountAsync(v => v.CustomerId == customerId, ct);
        var last = await db.Visits.Where(v => v.CustomerId == customerId && v.Status != VisitStatus.Cancelled).MaxAsync(v => (DateTime?)v.StartedAtUtc, ct);
        return new(summary, customer.Email, customer.Version, history.Take(25).ToList(), page, history.Count > 25, customer.CreatedAtUtc, count, last);
    }

    private static async Task<CustomerResult?> CheckDuplicatesAsync(OpticaDbContext db, CustomerData data,
        Guid? excluded, bool confirmed, CancellationToken ct)
    {
        var documentKey = CustomerData.DocumentKey(data.Document);
        var others = db.Customers.Where(c => c.Id != excluded);
        if (await others.AnyAsync(c => c.DocumentKey == documentKey, ct))
            return new(CustomerResultCode.DuplicateDocument, Error: "Ya existe un cliente con ese documento, activo o inactivo. Buscá y consultá su ficha.");
        if (confirmed) return null;
        var name = CustomerData.Fold($"{data.FirstName} {data.LastName}");
        var phone = data.Phone is null ? null : CustomerData.PhoneKey(data.Phone);
        var matches = await others.Where(c => c.SearchName == name || (phone != null && c.PhoneKey == phone))
            .OrderBy(c => c.LastName).Summaries(db).Take(10).ToListAsync(ct);
        return matches.Count == 0 ? null : new(CustomerResultCode.PossibleDuplicate,
            Error: "Hay coincidencias de nombre o teléfono. Revisá las fichas antes de continuar.", PossibleDuplicates: matches);
    }
}
