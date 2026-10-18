using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Inventory;
using Xunit;

namespace OpticaSosa.Infrastructure.Tests;

public sealed class StockDomainTests
{
    private static ProductVariant Variant(ProductCategory category) => new(Guid.NewGuid(), Guid.NewGuid(),
        new("SKU-FICTICIO", category, category == ProductCategory.ContactLens ? 6 : 1,
            contact: category == ProductCategory.ContactLens ? new(1m, 0m, null, null, 8.6m, 14.2m) : null), null, "actor-ficticio", DateTime.UtcNow);
    [Fact]
    public void ContactsRequireLotAndExpiryAndMonthKeepsItsPrecision()
    {
        var variant = Variant(ProductCategory.ContactLens);
        Assert.Throws<ArgumentException>(() => new StockBatch(Guid.NewGuid(), variant, 1, 0, null, new(2026, 10, 17), ExpiryPrecision.Day, "ficticio", DateTime.UtcNow));
        Assert.Throws<ArgumentException>(() => new StockBatch(Guid.NewGuid(), variant, 1, 0, "LOTE-FIC", null, ExpiryPrecision.None, "ficticio", DateTime.UtcNow));
        var batch = new StockBatch(Guid.NewGuid(), variant, 3, 1, "LOTE-FIC", new(2028, 2, 1), ExpiryPrecision.Month, "ficticio", DateTime.UtcNow);
        Assert.Equal(new DateOnly(2028, 2, 29), batch.ExpiresOn); Assert.Equal(ExpiryPrecision.Month, batch.ExpiryPrecision);
        Assert.Equal(2, batch.Available(new(2028, 2, 29))); Assert.Equal(0, batch.Available(new(2028, 3, 1)));
    }
    [Fact]
    public void ReservationDoesNotConsumePhysicalStockAndCannotUseQuarantine()
    {
        var today = new DateOnly(2026, 10, 17);
        var batch = new StockBatch(Guid.NewGuid(), Variant(ProductCategory.Frame), 3, 1, null, null, ExpiryPrecision.None, "ficticio", DateTime.UtcNow);
        batch.Reserve(2, today); Assert.Equal(3, batch.OnHand); Assert.Equal(0, batch.Available(today));
        Assert.Throws<ArgumentException>(() => batch.Reserve(1, today));
        batch.Release(1); Assert.Equal(3, batch.OnHand); Assert.Equal(1, batch.Available(today));
        batch.Consume(1, today); Assert.Equal(2, batch.OnHand); Assert.Equal(0, batch.Reserved); Assert.Equal(1, batch.Quarantined);
        Assert.Throws<ArgumentException>(() => batch.Release(1)); Assert.Throws<ArgumentException>(() => batch.Consume(1, today));
    }
    [Fact]
    public void ExpiryDoesNotSilentlyReleaseReservationsAndBlocksConsumptionTheFollowingDay()
    {
        var today = new DateOnly(2026, 10, 17);
        var batch = new StockBatch(Guid.NewGuid(), Variant(ProductCategory.ContactLens), 1, 0, "LOTE-FIC", today, ExpiryPrecision.Day, "ficticio", DateTime.UtcNow);
        batch.Reserve(1, today); Assert.Equal(1, batch.Reserved);
        Assert.Throws<ArgumentException>(() => batch.Consume(1, today.AddDays(1)));
        Assert.Equal(1, batch.Reserved); batch.Release(1); Assert.Equal(0, batch.Reserved);
        Assert.Equal(0, batch.Available(today.AddDays(1)));
    }
}
