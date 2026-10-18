using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Inventory;
using Xunit;

namespace OpticaSosa.Infrastructure.Tests;

public sealed class InventoryAdministrationDomainTests
{
    private static StockBatch Batch(ProductCategory category, DateOnly? expiry = null)
    {
        var specification = new VariantSpecification("SKU-FICTICIO", category, category == ProductCategory.ContactLens ? 6 : 1,
            contact: category == ProductCategory.ContactLens ? new(1m, 0m, null, null, 8.6m, 14.2m) : null);
        var variant = new ProductVariant(Guid.NewGuid(), Guid.NewGuid(), specification, null, "ficticio", DateTime.UtcNow);
        return new(Guid.NewGuid(), variant, 3, 1, "LOTE-FIC", expiry, expiry is null ? ExpiryPrecision.None : ExpiryPrecision.Day, "ficticio", DateTime.UtcNow);
    }
    [Fact]
    public void AdjustmentsCannotWithdrawReservationsAndHoldReleasesCannotExceedTheirOrigin()
    {
        var batch = Batch(ProductCategory.Frame); batch.Reserve(2, new(2026, 10, 17));
        Assert.Throws<ArgumentException>(() => batch.Adjust(-1, 0)); Assert.Throws<ArgumentException>(() => batch.Adjust(0, 1));
        var hold = new StockHold(Guid.NewGuid(), batch.Id, StockHoldOrigin.Receipt, 1, false, "Ficticio", "ficticio", DateTime.UtcNow);
        Assert.Throws<ArgumentException>(() => hold.Release(2));
        hold.Remove(1); Assert.Equal(0, hold.PendingQuantity); Assert.Throws<ArgumentException>(() => hold.Release(1));
    }
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void OpenedOrExpiredContactsNeverBecomeUsableEvenWhenSomeoneChecksTheApprovalBoxes(bool opened, bool expired)
    {
        var today = new DateOnly(2026, 10, 17); var batch = Batch(ProductCategory.ContactLens, expired ? today.AddDays(-1) : today.AddDays(1));
        var hold = new StockHold(Guid.NewGuid(), batch.Id, StockHoldOrigin.CustomerReturn, 1, opened, "Ficticio", "ficticio", DateTime.UtcNow);
        Assert.Throws<ArgumentException>(() => new StockReview(Guid.NewGuid(), hold, batch, 1, true, true, true, true, "Ficticio",
            new string('0', 64), "ficticio", StockReviewerRole.Professional, DateTime.UtcNow, today));
    }
    [Fact]
    public void ClinicalReviewRequiresProfessionalAndExplicitSealConfirmationForContacts()
    {
        var today = new DateOnly(2026, 10, 17); var batch = Batch(ProductCategory.ContactLens, today.AddDays(1));
        var hold = new StockHold(Guid.NewGuid(), batch.Id, StockHoldOrigin.Receipt, 1, false, "Ficticio", "ficticio", DateTime.UtcNow);
        Assert.Throws<ArgumentException>(() => new StockReview(Guid.NewGuid(), hold, batch, 1, true, true, true, true, "Ficticio",
            new string('0', 64), "ficticio", StockReviewerRole.Sales, DateTime.UtcNow, today));
        Assert.Throws<ArgumentException>(() => new StockReview(Guid.NewGuid(), hold, batch, 1, true, true, true, null, "Ficticio",
            new string('0', 64), "ficticio", StockReviewerRole.Professional, DateTime.UtcNow, today));
    }
}
