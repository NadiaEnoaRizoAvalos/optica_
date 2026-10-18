using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Domain.Work;
using OpticaSosa.Infrastructure.Identity;

namespace OpticaSosa.Infrastructure.Persistence.Configurations;

public sealed class StockHoldConfiguration : IEntityTypeConfiguration<StockHold>
{
    public void Configure(EntityTypeBuilder<StockHold> e)
    {
        e.HasKey(x => x.Id); e.Property(x => x.Version).IsConcurrencyToken(); e.Ignore(x => x.PendingQuantity);
        e.HasOne<StockBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.BatchId, x.CreatedAtUtc, x.Id }); e.Property(x => x.Reason).HasMaxLength(500);
        e.ToTable("StockHolds", t => t.HasCheckConstraint("CK_StockHolds_Profile", "Origin IN (1,2,3,4) AND typeof(Quantity) = 'integer' AND Quantity > 0 AND typeof(ReleasedQuantity) = 'integer' AND ReleasedQuantity >= 0 AND typeof(RemovedQuantity) = 'integer' AND RemovedQuantity >= 0 AND ReleasedQuantity + RemovedQuantity <= Quantity AND OpenedContact IN (0,1) AND (OpenedContact = 0 OR ReleasedQuantity = 0) AND length(trim(Reason)) BETWEEN 1 AND 500"));
    }
}
public sealed class StockReviewConfiguration : IEntityTypeConfiguration<StockReview>
{
    public void Configure(EntityTypeBuilder<StockReview> e)
    {
        e.HasKey(x => x.Id);
        e.HasOne<StockHold>().WithMany().HasForeignKey(x => x.HoldId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.HoldId, x.ReviewedAtUtc, x.Id }); e.Property(x => x.Reason).HasMaxLength(500); e.Property(x => x.RequestHash).HasMaxLength(64);
        e.ToTable("StockReviews", t => t.HasCheckConstraint("CK_StockReviews_Profile", "typeof(Quantity) = 'integer' AND Quantity > 0 AND Role IN (1,2) AND Approved IN (0,1) AND ProductMatches IN (0,1) AND ItemIntact IN (0,1) AND (SealIntact IS NULL OR SealIntact IN (0,1)) AND (Approved = 0 OR (ProductMatches = 1 AND ItemIntact = 1)) AND length(trim(Reason)) BETWEEN 1 AND 500 AND length(RequestHash) = 64 AND RequestHash NOT GLOB '*[^0-9A-F]*'"));
    }
}
public sealed class StockCustomerReturnConfiguration : IEntityTypeConfiguration<StockCustomerReturn>
{
    public void Configure(EntityTypeBuilder<StockCustomerReturn> e)
    {
        e.HasKey(x => x.Id);
        e.HasOne<StockReservation>().WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<StockHold>().WithMany().HasForeignKey(x => x.HoldId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => x.HoldId).IsUnique(); e.HasIndex(x => new { x.ReservationId, x.ReturnedAtUtc, x.Id }); e.Property(x => x.Reason).HasMaxLength(500);
        e.ToTable("StockCustomerReturns", t => t.HasCheckConstraint("CK_StockCustomerReturns_Profile", "typeof(Quantity) = 'integer' AND Quantity > 0 AND (ContactPackageOpened IS NULL OR ContactPackageOpened IN (0,1)) AND length(trim(Reason)) BETWEEN 1 AND 500"));
    }
}
public sealed class StockWithdrawalConfiguration : IEntityTypeConfiguration<StockWithdrawal>
{
    public void Configure(EntityTypeBuilder<StockWithdrawal> e)
    {
        e.HasKey(x => x.Id);
        e.HasOne<StockMovement>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<StockBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<StockHold>().WithMany().HasForeignKey(x => x.HoldId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        e.ToTable("StockWithdrawals", t => t.HasCheckConstraint("CK_StockWithdrawals_Profile", "typeof(Quantity) = 'integer' AND Quantity > 0 AND ((Kind = 6 AND SupplierId IS NULL) OR (Kind = 10 AND SupplierId IS NOT NULL))"));
    }
}
public sealed class StockDeliveryProofConfiguration : IEntityTypeConfiguration<StockDeliveryProof>
{
    public void Configure(EntityTypeBuilder<StockDeliveryProof> e)
    {
        e.HasKey(x => x.ReservationId);
        e.HasOne<StockReservation>().WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<OpticalWork>().WithMany().HasForeignKey(x => x.WorkId).OnDelete(DeleteBehavior.Restrict);
    }
}
