using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Customers;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Customers;
using OpticaSosa.Infrastructure.Persistence;

namespace OpticaSosa.Infrastructure.Customers;

public sealed class VisitService(IDbContextFactory<OpticaDbContext> factory, ICurrentUser current, TimeProvider clock) : IVisitService
{
    public async Task<CustomerResult> StartAsync(Guid customerId, VisitInput input, CancellationToken ct = default)
    {
        var actor = CustomerOperations.Demand(current, Permissions.WriteCustomers);
        string reason;
        string? notes;
        try
        {
            reason = CustomerData.Required(input.Reason, 100, "motivo");
            notes = CustomerData.Optional(input.AdministrativeNotes, 500, "observaciones administrativas");
        }
        catch (ArgumentException ex) { return new(CustomerResultCode.Invalid, Error: ex.Message); }
        await using var db = await factory.CreateDbContextAsync(ct);
        var previous = await db.Visits.FindAsync([input.SubmissionId], ct);
        if (previous is not null)
            return previous.CustomerId == customerId && previous.Reason == reason && previous.AdministrativeNotes == notes
                ? new(CustomerResultCode.Success, customerId) : CustomerOperations.Conflict();
        var customer = await db.Customers.FindAsync([customerId], ct);
        if (customer is null) return CustomerOperations.Missing();
        if (!customer.IsActive) return new(CustomerResultCode.Invalid, Error: "No se pueden iniciar visitas de clientes inactivos.");
        if (customer.Version != input.CustomerVersion) return CustomerOperations.Conflict();
        if (await db.Visits.AnyAsync(v => v.CustomerId == customerId && v.Status == VisitStatus.InProgress, ct))
            return new(CustomerResultCode.Invalid, Error: "El cliente ya tiene una visita en curso.");
        var now = clock.GetUtcNow().UtcDateTime;
        var recurrent = await db.Visits.AnyAsync(v => v.CustomerId == customerId && v.Status == VisitStatus.Completed && v.EndedAtUtc < now, ct);
        Visit visit;
        try { visit = new Visit(input.SubmissionId, customerId, reason, notes, actor, now, recurrent); }
        catch (ArgumentException ex) { return new(CustomerResultCode.Invalid, Error: ex.Message); }
        customer.Touch();
        db.Visits.Add(visit);
        CustomerOperations.Audit(db, actor, "visit.started", visit.Id);
        return await CustomerOperations.SaveAsync(db, customerId, ct);
    }

    public async Task<CustomerResult> CloseAsync(Guid customerId, Guid visitId, Guid version, bool completed, CancellationToken ct = default)
    {
        var actor = CustomerOperations.Demand(current, Permissions.WriteCustomers);
        await using var db = await factory.CreateDbContextAsync(ct);
        var visit = await db.Visits.SingleOrDefaultAsync(v => v.Id == visitId && v.CustomerId == customerId, ct);
        if (visit is null) return new(CustomerResultCode.NotFound, Error: "La visita no corresponde a este cliente.");
        var desired = completed ? VisitStatus.Completed : VisitStatus.Cancelled;
        if (visit.Status == desired) return new(CustomerResultCode.Success, customerId);
        if (visit.Version != version) return CustomerOperations.Conflict();
        try { visit.Close(completed, actor, clock.GetUtcNow().UtcDateTime); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        { return new(CustomerResultCode.Invalid, Error: ex.Message); }
        CustomerOperations.Audit(db, actor, completed ? "visit.completed" : "visit.cancelled", visit.Id);
        return await CustomerOperations.SaveAsync(db, customerId, ct);
    }
}
