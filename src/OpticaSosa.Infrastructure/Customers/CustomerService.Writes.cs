using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Customers;
using OpticaSosa.Application.Security;
using OpticaSosa.Domain.Customers;

namespace OpticaSosa.Infrastructure.Customers;

public sealed partial class CustomerService
{
    public async Task<CustomerResult> CreateAsync(CustomerInput input, CancellationToken ct = default)
    {
        var actor = CustomerOperations.Demand(current, Permissions.WriteCustomers);
        CustomerData data;
        try { data = input.ToData(); }
        catch (ArgumentException ex) { return new(CustomerResultCode.Invalid, Error: ex.Message); }
        if (input.SubmissionId == Guid.Empty) return new(CustomerResultCode.Invalid, Error: "Recargá el formulario para registrar al cliente.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var previous = await db.Customers.FindAsync([input.SubmissionId], ct);
        if (previous is not null)
            return previous.FirstName == data.FirstName && previous.LastName == data.LastName &&
                previous.DocumentKey == CustomerData.DocumentKey(data.Document) && previous.Phone == data.Phone && previous.Email == data.Email
                ? new(CustomerResultCode.Success, previous.Id) : CustomerOperations.Conflict();
        var duplicate = await CheckDuplicatesAsync(db, data, null, input.ConfirmPossibleDuplicate, ct);
        if (duplicate is not null) return duplicate;
        var customer = new Customer(input.SubmissionId, data, actor, clock.GetUtcNow().UtcDateTime);
        db.Customers.Add(customer);
        CustomerOperations.Audit(db, actor, "customer.created", customer.Id);
        return await CustomerOperations.SaveAsync(db, customer.Id, ct);
    }

    public async Task<CustomerResult> UpdateAsync(Guid customerId, CustomerInput input, CancellationToken ct = default)
    {
        var actor = CustomerOperations.Demand(current, Permissions.WriteCustomers);
        CustomerData data;
        try { data = input.ToData(); }
        catch (ArgumentException ex) { return new(CustomerResultCode.Invalid, Error: ex.Message); }
        await using var db = await factory.CreateDbContextAsync(ct);
        var customer = await db.Customers.FindAsync([customerId], ct);
        if (customer is null) return CustomerOperations.Missing();
        if (customer.Version != input.Version) return CustomerOperations.Conflict();
        var duplicate = await CheckDuplicatesAsync(db, data, customerId, input.ConfirmPossibleDuplicate, ct);
        if (duplicate is not null) return duplicate;
        customer.Update(data);
        CustomerOperations.Audit(db, actor, "customer.updated", customer.Id);
        return await CustomerOperations.SaveAsync(db, customer.Id, ct);
    }

    public async Task<CustomerResult> DeactivateAsync(Guid customerId, Guid version, CancellationToken ct = default)
    {
        var actor = CustomerOperations.Demand(current, Permissions.DeactivateCustomers);
        await using var db = await factory.CreateDbContextAsync(ct);
        var customer = await db.Customers.FindAsync([customerId], ct);
        if (customer is null) return CustomerOperations.Missing();
        if (!customer.IsActive) return new(CustomerResultCode.Success, customerId);
        if (customer.Version != version) return CustomerOperations.Conflict();
        if (await db.Visits.AnyAsync(v => v.CustomerId == customerId && v.Status == VisitStatus.InProgress, ct))
            return new(CustomerResultCode.Invalid, Error: "Finalizá o cancelá la visita en curso antes de inactivar al cliente.");
        customer.Deactivate();
        CustomerOperations.Audit(db, actor, "customer.deactivated", customer.Id);
        return await CustomerOperations.SaveAsync(db, customer.Id, ct);
    }
}
