using Microsoft.EntityFrameworkCore;
using OpticaSosa.Application.Customers;
using OpticaSosa.Domain.Customers;
using Xunit;

namespace OpticaSosa.Web.Tests;

public sealed class CustomerPersistenceTests
{
    [Fact]
    public async Task DocumentsAreMandatoryUniqueAndRemainReservedForInactiveCustomers()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var scenario = await CustomerScenario.OpenAsync(app);
        var missing = CustomerScenario.Input("");
        Assert.Equal(CustomerResultCode.Invalid, (await scenario.Customers.CreateAsync(missing)).Code);
        var input = CustomerScenario.Input();
        var created = await scenario.Customers.CreateAsync(input);
        Assert.True(created.Succeeded);
        Assert.True((await scenario.Customers.CreateAsync(input)).Succeeded);
        var duplicate = CustomerScenario.Input("fic.001"); duplicate.ConfirmPossibleDuplicate = true;
        Assert.Equal(CustomerResultCode.DuplicateDocument, (await scenario.Customers.CreateAsync(duplicate)).Code);
        var details = (await scenario.Customers.GetAsync(created.CustomerId!.Value))!;
        Assert.True((await scenario.Customers.DeactivateAsync(details.Customer.Id, details.Version)).Succeeded);
        Assert.Equal(CustomerResultCode.DuplicateDocument, (await scenario.Customers.CreateAsync(duplicate)).Code);
        Assert.Empty((await scenario.Customers.SearchAsync(null)).Items);
        Assert.Single((await scenario.Customers.SearchAsync(null, true)).Items);
        Assert.Equal(1, await scenario.Db.Customers.CountAsync());
        Assert.Equal(1, await scenario.Db.AuditEntries.CountAsync(x => x.Action == "customer.created"));
    }

    [Fact]
    public async Task NameOrPhoneMatchesRequireConfirmationAndSearchIgnoresAccentsAndNameOrder()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var scenario = await CustomerScenario.OpenAsync(app);
        var input = CustomerScenario.Input(); input.FirstName = "Lucía ficticia"; input.Phone = "000-000-0101";
        input.Email = "cliente@ejemplo.invalid";
        Assert.True((await scenario.Customers.CreateAsync(input)).Succeeded);
        foreach (var term in new[] { "prueba lucia", "LUCIA", "fic001", "0000101", "@ejemplo.invalid" })
            Assert.Single((await scenario.Customers.SearchAsync(term)).Items);
        var second = CustomerScenario.Input("FIC-002"); second.FirstName = input.FirstName;
        Assert.Equal(CustomerResultCode.PossibleDuplicate, (await scenario.Customers.CreateAsync(second)).Code);
        second.ConfirmPossibleDuplicate = true;
        Assert.True((await scenario.Customers.CreateAsync(second)).Succeeded);
        var third = CustomerScenario.Input("FIC-003"); third.Phone = input.Phone;
        Assert.Equal(CustomerResultCode.PossibleDuplicate, (await scenario.Customers.CreateAsync(third)).Code);
        Assert.Equal(2, await scenario.Db.Customers.CountAsync());
    }

    [Fact]
    public async Task StaleEditsAndDuplicateDocumentsCannotOverwriteAnotherCustomersData()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var scenario = await CustomerScenario.OpenAsync(app);
        var first = CustomerScenario.Input(); var other = CustomerScenario.Input("FIC-002");
        await scenario.Customers.CreateAsync(first); await scenario.Customers.CreateAsync(other);
        var snapshot = (await scenario.Customers.GetAsync(first.SubmissionId))!;
        first.Version = snapshot.Version; first.FirstName = "Nombre corregido ficticio";
        Assert.True((await scenario.Customers.UpdateAsync(first.SubmissionId, first)).Succeeded);
        first.LastName = "No debe persistir";
        Assert.Equal(CustomerResultCode.Conflict, (await scenario.Customers.UpdateAsync(first.SubmissionId, first)).Code);
        Assert.Equal("Prueba", (await scenario.Customers.GetAsync(first.SubmissionId))!.Customer.LastName);
        first.Version = (await scenario.Customers.GetAsync(first.SubmissionId))!.Version;
        first.Document = other.Document;
        Assert.Equal(CustomerResultCode.DuplicateDocument, (await scenario.Customers.UpdateAsync(first.SubmissionId, first)).Code);
        scenario.Db.Customers.Add(new Customer(Guid.NewGuid(), other.ToData(), scenario.ActorId, DateTime.UtcNow));
        await Assert.ThrowsAsync<DbUpdateException>(() => scenario.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task AuditRecordsIdsAndActionsWithoutCopyingPersonalData()
    {
        await using var app = new SecurityApp(); await app.InitializeAsync();
        using var scenario = await CustomerScenario.OpenAsync(app);
        var input = CustomerScenario.Input(); await scenario.Customers.CreateAsync(input);
        input.Version = (await scenario.Customers.GetAsync(input.SubmissionId))!.Version;
        input.FirstName = "Otro nombre ficticio";
        await scenario.Customers.UpdateAsync(input.SubmissionId, input);
        var events = await scenario.Db.AuditEntries.Where(x => x.Action.StartsWith("customer.")).ToListAsync();
        Assert.Contains(events, x => x.Action == "customer.created");
        Assert.Contains(events, x => x.Action == "customer.updated");
        Assert.All(events, x => { Assert.Equal(scenario.ActorId, x.ActorId); Assert.Equal(input.SubmissionId.ToString(), x.TargetId); });
        var serialized = System.Text.Json.JsonSerializer.Serialize(events);
        Assert.DoesNotContain(input.Document, serialized);
        Assert.DoesNotContain(input.FirstName, serialized);
    }
}
