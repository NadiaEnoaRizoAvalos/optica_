using OpticaSosa.Domain.Catalog;
using Xunit;

namespace OpticaSosa.Infrastructure.Tests;

public sealed class SupplierDomainTests
{
    [Theory]
    [InlineData("", true, false)]
    [InlineData("Ficticio", false, false)]
    [InlineData("Nombre\u0001", true, true)]
    public void InvalidSupplierProfilesAreRejectedWithoutPersistence(string name, bool supplier, bool lab) =>
        Assert.Throws<ArgumentException>(() => new SupplierProfile(name, supplier, lab));

    [Theory]
    [InlineData("no-es-correo", null)]
    [InlineData(null, "no-es-telefono")]
    public void ContactDataIsValidated(string? email, string? phone) =>
        Assert.Throws<ArgumentException>(() => new SupplierProfile("Ficticio", true, false, phone, email));

    [Fact]
    public void SupplierHasStableIdentityAndOptimisticVersionWithoutCreatingAccessAccounts()
    {
        var id = Guid.NewGuid(); var profile = new SupplierProfile(" Ficticio   de prueba ", true, true);
        var supplier = new Supplier(id, profile, "actor-ficticio", DateTime.UtcNow);
        var version = supplier.Version;
        supplier.Update(profile, false);
        Assert.Equal(id, supplier.Id); Assert.NotEqual(version, supplier.Version);
        Assert.Equal("Ficticio de prueba", supplier.Name); Assert.False(supplier.IsActive);
        Assert.True(supplier.Matches(profile));
    }
}
