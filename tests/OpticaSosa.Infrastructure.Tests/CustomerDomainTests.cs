using OpticaSosa.Domain.Customers;
using Xunit;

namespace OpticaSosa.Infrastructure.Tests;

public sealed class CustomerDomainTests
{
    [Theory]
    [InlineData("", "Prueba", "FIC001")]
    [InlineData("Nombre ficticio", "", "FIC001")]
    [InlineData("Nombre ficticio", "Prueba", "")]
    [InlineData("Nombre ficticio", "Prueba", "---")]
    [InlineData("Nombre ficticio", "Prueba", "<FIC>")]
    public void InvalidRequiredDataCannotCreateCustomers(string name, string lastName, string document) =>
        Assert.Throws<ArgumentException>(() => new CustomerData(name, lastName, document, null, null));

    [Fact]
    public void CompletedVisitCannotBeReopenedOrCancelledAndDatesRemainConsistent()
    {
        var now = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        var visit = new Visit(Guid.NewGuid(), Guid.NewGuid(), "Consulta comercial ficticia", null, "synthetic-actor", now, false);
        Assert.Throws<ArgumentException>(() => visit.Close(true, "synthetic-actor", now.AddMinutes(-1)));
        visit.Close(true, "synthetic-actor", now.AddMinutes(10));
        Assert.Throws<InvalidOperationException>(() => visit.Close(false, "synthetic-actor", now.AddMinutes(11)));
        Assert.False(visit.WasRecurrent);
    }
}
