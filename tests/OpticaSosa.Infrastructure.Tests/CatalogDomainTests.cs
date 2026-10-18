using OpticaSosa.Domain.Catalog;
using Xunit;

namespace OpticaSosa.Infrastructure.Tests;

public sealed class CatalogDomainTests
{
    [Theory]
    [InlineData("12.345", 1235L)]
    [InlineData("12.344", 1234L)]
    [InlineData("0", 0L)]
    public void MoneyUsesExactMinorUnitsAndApprovedRounding(string amount, long cents)
    {
        var money = Money.FromDecimal(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), " ars ");
        Assert.Equal(cents, money.MinorUnits); Assert.Equal("ARS", money.Currency);
        Assert.Equal(cents / 100m, money.Amount);
    }
    [Fact]
    public void MoneyRejectsNegativeAmountsOverflowAndInvalidCodes()
    {
        Assert.Throws<ArgumentException>(() => Money.FromDecimal(-1m, "ARS"));
        Assert.Throws<ArgumentException>(() => Money.FromDecimal(decimal.MaxValue, "ARS"));
        Assert.Throws<ArgumentException>(() => new Money(1, "A1S"));
    }
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ContactsRequireDeclaredClosedBoxCount(int count) =>
        Assert.Throws<ArgumentException>(() => new VariantSpecification("FIC", ProductCategory.ContactLens, count));
    [Fact]
    public void MissingPowersDifferFromZeroAndPrecisionIsPreserved()
    {
        var spec = new VariantSpecification("FIC-CON", ProductCategory.ContactLens, 6,
            contact: new(1.125m, 0m, 180, null, 8.60m, 14.20m));
        Assert.Equal(ProductUnit.ClosedBox, spec.Unit); Assert.Equal(6, spec.UnitsPerPackage);
        Assert.Equal(0m, spec.Contact!.Cylinder); Assert.Null(spec.Contact.Addition);
        Assert.Throws<ArgumentException>(() => new VariantSpecification("FIC-CON", ProductCategory.ContactLens, 6, contact: new(1.1234m)));
        Assert.Throws<ArgumentException>(() => new VariantSpecification("FIC-CON", ProductCategory.ContactLens, 6, contact: new(BaseCurveMm: 8.601m)));
        Assert.Throws<ArgumentException>(() => new VariantSpecification("FIC-CON", ProductCategory.ContactLens, 6, contact: new(Axis: 181)));
        Assert.Throws<ArgumentException>(() => new VariantSpecification("FIC-CON", ProductCategory.ContactLens));
    }
    [Fact]
    public void CategoryAndPackagingPreventMixingProductFieldsOrSellingPairsImplicitly()
    {
        Assert.Throws<ArgumentException>(() => new VariantSpecification("FIC", ProductCategory.Frame, lens: new()));
        Assert.Throws<ArgumentException>(() => new VariantSpecification("FIC", ProductCategory.Lens, 2));
        Assert.Equal(1, new VariantSpecification("FIC", ProductCategory.Lens).UnitsPerPackage);
        Assert.Equal(ProductUnit.Each, new VariantSpecification("FIC", ProductCategory.Lens).Unit);
    }
    [Fact]
    public void UsedSpecificationsAreImmutableWhilePriceAndActivityCanChange()
    {
        var spec = new VariantSpecification("FIC-FR", ProductCategory.Frame, frame: new("Azul ficticio", 52, 18, 140));
        var originalPrice = new Money(10000, "ARS");
        var variant = new ProductVariant(Guid.NewGuid(), Guid.NewGuid(), spec, originalPrice, "actor-ficticio", DateTime.UtcNow);
        variant.LockSpecification();
        Assert.Throws<ArgumentException>(() => variant.Update(new("FIC-OTRO", ProductCategory.Frame), originalPrice, true));
        variant.Update(spec, new Money(15000, "ARS"), false);
        Assert.True(variant.SpecificationLocked); Assert.False(variant.IsActive);
        Assert.Equal(10000, originalPrice.MinorUnits); Assert.Equal(15000, variant.Price()!.MinorUnits);
    }
}
