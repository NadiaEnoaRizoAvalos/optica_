using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Commerce;
using OpticaSosa.Domain.Customers;
using OpticaSosa.Infrastructure.Identity;

namespace OpticaSosa.Infrastructure.Persistence.Configurations;

public sealed class SalesOrderConfiguration : IEntityTypeConfiguration<SalesOrder>
{
    public void Configure(EntityTypeBuilder<SalesOrder> e)
    {
        e.HasKey(x => x.Id); e.HasAlternateKey(x => new { x.Id, x.Currency });
        e.Property(x => x.Version).IsConcurrencyToken(); e.Property(x => x.Currency).HasMaxLength(3);
        e.Property(x => x.CancellationReason).HasMaxLength(500);
        e.Ignore(x => x.NetPaidMinor); e.Ignore(x => x.BalanceMinor);
        e.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ConfirmedByUserId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CancelledByUserId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.CustomerId, x.CreatedAtUtc, x.Id });
        e.HasIndex(x => new { x.CreatedAtUtc, x.Id });
        e.ToTable("SalesOrders", t =>
        {
            t.HasCheckConstraint("CK_SalesOrders_Amounts", "typeof(TotalMinor) = 'integer' AND TotalMinor >= 0 AND typeof(ReceivedMinor) = 'integer' AND ReceivedMinor >= 0 AND typeof(RefundedMinor) = 'integer' AND RefundedMinor BETWEEN 0 AND ReceivedMinor AND ReceivedMinor - RefundedMinor <= TotalMinor");
            t.HasCheckConstraint("CK_SalesOrders_Profile", "Status IN (1,2,3) AND Currency GLOB '[A-Z][A-Z][A-Z]' AND typeof(DraftNumber) = 'integer' AND DraftNumber > 0");
            t.HasCheckConstraint("CK_SalesOrders_Confirmation", "(ConfirmedAtUtc IS NULL AND ConfirmedByUserId IS NULL AND Status <> 2) OR (ConfirmedAtUtc IS NOT NULL AND ConfirmedByUserId IS NOT NULL AND Status <> 1)");
            t.HasCheckConstraint("CK_SalesOrders_Cancellation", "(Status <> 3 AND CancelledAtUtc IS NULL AND CancelledByUserId IS NULL AND CancellationReason IS NULL) OR (Status = 3 AND CancelledAtUtc IS NOT NULL AND CancelledByUserId IS NOT NULL AND CancellationReason IS NOT NULL AND length(trim(CancellationReason)) > 0 AND ReceivedMinor = RefundedMinor)");
        });
    }
}

public sealed class SalesLineConfiguration : IEntityTypeConfiguration<SalesLine>
{
    public void Configure(EntityTypeBuilder<SalesLine> e)
    {
        e.HasKey(x => x.Id);
        e.HasOne<SalesOrder>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ProductVariant>().WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.OrderId, x.DraftNumber, x.Position }).IsUnique();
        e.Property(x => x.ProductName).HasMaxLength(160); e.Property(x => x.VariantCode).HasMaxLength(80); e.Property(x => x.DiscountReason).HasMaxLength(500);
        e.ToTable("SalesLines", t =>
        {
            t.HasCheckConstraint("CK_SalesLines_Profile", "typeof(DraftNumber) = 'integer' AND DraftNumber > 0 AND typeof(Position) = 'integer' AND Position > 0 AND typeof(Quantity) = 'integer' AND Quantity > 0 AND Source IN (1,2) AND Category BETWEEN 1 AND 4 AND length(trim(ProductName)) BETWEEN 1 AND 160 AND length(trim(VariantCode)) BETWEEN 1 AND 80");
            t.HasCheckConstraint("CK_SalesLines_Units", "(Category = 3 AND Unit = 2 AND typeof(UnitsPerPackage) = 'integer' AND UnitsPerPackage > 0) OR (Category IN (1,2,4) AND Unit = 1 AND UnitsPerPackage = 1)");
            t.HasCheckConstraint("CK_SalesLines_Amounts", "typeof(UnitPriceMinor) = 'integer' AND UnitPriceMinor >= 0 AND typeof(DiscountMinor) = 'integer' AND DiscountMinor >= 0 AND typeof(UnitPriceMinor * Quantity) = 'integer' AND DiscountMinor <= UnitPriceMinor * Quantity AND typeof(TotalMinor) = 'integer' AND TotalMinor = UnitPriceMinor * Quantity - DiscountMinor");
            t.HasCheckConstraint("CK_SalesLines_Discount", "DiscountMinor = 0 OR (DiscountReason IS NOT NULL AND length(trim(DiscountReason)) > 0)");
        });
    }
}

public sealed class OrderMoneyEntryConfiguration : IEntityTypeConfiguration<OrderMoneyEntry>
{
    public void Configure(EntityTypeBuilder<OrderMoneyEntry> e)
    {
        e.HasKey(x => x.Id); e.HasAlternateKey(x => new { x.Id, x.OrderId });
        e.HasOne<SalesOrder>().WithMany().HasForeignKey(x => new { x.OrderId, x.Currency })
            .HasPrincipalKey(x => new { x.Id, x.Currency }).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<OrderMoneyEntry>().WithMany().HasForeignKey(x => new { x.OriginalPaymentId, x.OrderId })
            .HasPrincipalKey(x => new { x.Id, x.OrderId }).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.OrderId, x.OccurredAtUtc, x.Id });
        e.Property(x => x.Currency).HasMaxLength(3); e.Property(x => x.Method).HasMaxLength(80);
        e.Property(x => x.Reference).HasMaxLength(160); e.Property(x => x.Reason).HasMaxLength(500);
        e.ToTable("OrderMoneyEntries", t =>
        {
            t.HasCheckConstraint("CK_OrderMoneyEntries_Profile", "typeof(AmountMinor) = 'integer' AND AmountMinor > 0 AND Currency GLOB '[A-Z][A-Z][A-Z]' AND length(trim(Method)) BETWEEN 1 AND 80");
            t.HasCheckConstraint("CK_OrderMoneyEntries_Original", "(Kind = 1 AND OriginalPaymentId IS NULL AND Reason IS NULL) OR (Kind = 2 AND OriginalPaymentId IS NOT NULL AND OriginalPaymentId <> Id AND Reason IS NOT NULL AND length(trim(Reason)) > 0)");
        });
    }
}

public sealed class OrderCommandReceiptConfiguration : IEntityTypeConfiguration<OrderCommandReceipt>
{
    public void Configure(EntityTypeBuilder<OrderCommandReceipt> e)
    {
        e.HasKey(x => x.Id);
        e.HasOne<SalesOrder>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        e.Property(x => x.Action).HasMaxLength(80); e.Property(x => x.RequestHash).HasMaxLength(64);
        e.ToTable("OrderCommandReceipts", t => t.HasCheckConstraint("CK_OrderCommandReceipts_Profile", "length(trim(Action)) BETWEEN 1 AND 80 AND length(RequestHash) = 64 AND RequestHash NOT GLOB '*[^0-9A-F]*'"));
    }
}
