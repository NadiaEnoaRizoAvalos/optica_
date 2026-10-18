using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Commerce;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Domain.Technical;
using OpticaSosa.Infrastructure.Identity;

namespace OpticaSosa.Infrastructure.Persistence.Configurations;

public sealed class StockBatchConfiguration : IEntityTypeConfiguration<StockBatch>
{
    public void Configure(EntityTypeBuilder<StockBatch> e)
    {
        e.HasKey(x => x.Id); e.Property(x => x.Version).IsConcurrencyToken();
        e.Property(x => x.Lot).HasMaxLength(100); e.Property(x => x.LotKey).HasMaxLength(100);
        e.HasOne<ProductVariant>().WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReceivedByUserId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.VariantId, x.ExpiresOn, x.Id }); e.HasIndex(x => new { x.VariantId, x.LotKey });
        e.ToTable("StockBatches", t =>
        {
            t.HasCheckConstraint("CK_StockBatches_Counts", "typeof(OnHand) = 'integer' AND OnHand >= 0 AND typeof(Reserved) = 'integer' AND Reserved >= 0 AND typeof(Quarantined) = 'integer' AND Quarantined >= 0 AND Reserved + Quarantined <= OnHand");
            t.HasCheckConstraint("CK_StockBatches_Expiry", "(ExpiryPrecision = 0 AND ExpiresOn IS NULL) OR (ExpiryPrecision IN (1,2) AND ExpiresOn IS NOT NULL AND (ExpiryPrecision <> 2 OR ExpiresOn = date(ExpiresOn, 'start of month', '+1 month', '-1 day')))");
            t.HasCheckConstraint("CK_StockBatches_Contact", "Category BETWEEN 1 AND 4 AND (Category <> 3 OR (Lot IS NOT NULL AND length(trim(Lot)) > 0 AND LotKey IS NOT NULL AND ExpiresOn IS NOT NULL))");
        });
    }
}

public sealed class OrderTechnicalAssignmentConfiguration : IEntityTypeConfiguration<OrderTechnicalAssignment>
{
    public void Configure(EntityTypeBuilder<OrderTechnicalAssignment> e)
    {
        e.HasKey(x => x.Id); e.Property(x => x.Version).IsConcurrencyToken();
        e.HasOne<SalesLine>().WithMany().HasForeignKey(x => x.LineId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<TechnicalPublication>().WithMany().HasForeignKey(x => x.PublicationId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<OrderTechnicalAssignment>().WithMany().HasForeignKey(x => x.PreviousId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AssignedByUserId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => x.LineId).IsUnique().HasFilter("IsCurrent = 1");
        e.HasIndex(x => x.PreviousId).IsUnique(); e.Property(x => x.ReplacementReason).HasMaxLength(500);
        e.ToTable("OrderTechnicalAssignments", t =>
        {
            t.HasCheckConstraint("CK_OrderTechnicalAssignments_Profile", "Eyes IN (1,2,3) AND IsCurrent IN (0,1) AND (PreviousId IS NULL OR (PreviousId <> Id AND ReplacementReason IS NOT NULL AND length(trim(ReplacementReason)) > 0))");
        });
    }
}

public sealed class StockReservationConfiguration : IEntityTypeConfiguration<StockReservation>
{
    public void Configure(EntityTypeBuilder<StockReservation> e)
    {
        e.HasKey(x => x.Id); e.Property(x => x.Version).IsConcurrencyToken();
        e.HasOne<SalesLine>().WithMany().HasForeignKey(x => x.LineId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<StockBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<OrderTechnicalAssignment>().WithMany().HasForeignKey(x => x.TechnicalAssignmentId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ClosedByUserId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.LineId, x.Status }); e.Property(x => x.ClosureReason).HasMaxLength(500);
        e.ToTable("StockReservations", t =>
        {
            t.HasCheckConstraint("CK_StockReservations_Quantity", "typeof(Quantity) = 'integer' AND Quantity > 0");
            t.HasCheckConstraint("CK_StockReservations_Closure", "(Status = 1 AND ClosedAtUtc IS NULL AND ClosedByUserId IS NULL AND ClosureReason IS NULL) OR (Status IN (2,3) AND ClosedAtUtc IS NOT NULL AND ClosedByUserId IS NOT NULL AND ClosureReason IS NOT NULL AND length(trim(ClosureReason)) > 0)");
        });
    }
}

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> e)
    {
        e.HasKey(x => x.Id);
        e.HasOne<StockBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<StockReservation>().WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        e.Property(x => x.Reason).HasMaxLength(500); e.Property(x => x.RequestHash).HasMaxLength(64);
        e.HasIndex(x => new { x.BatchId, x.OccurredAtUtc, x.Id });
        e.ToTable("StockMovements", t =>
        {
            t.HasCheckConstraint("CK_StockMovements_Metadata", "length(trim(Reason)) BETWEEN 1 AND 500 AND length(RequestHash) = 64 AND RequestHash NOT GLOB '*[^0-9A-F]*'");
            t.HasCheckConstraint("CK_StockMovements_Deltas", "typeof(PhysicalDelta) = 'integer' AND typeof(ReservedDelta) = 'integer' AND typeof(QuarantineDelta) = 'integer' AND ((Kind = 1 AND ReservationId IS NULL AND PhysicalDelta > 0 AND ReservedDelta = 0 AND QuarantineDelta BETWEEN 0 AND PhysicalDelta) OR (Kind = 2 AND ReservationId IS NOT NULL AND PhysicalDelta = 0 AND ReservedDelta > 0 AND QuarantineDelta = 0) OR (Kind = 3 AND ReservationId IS NOT NULL AND PhysicalDelta = 0 AND ReservedDelta < 0 AND QuarantineDelta = 0) OR (Kind = 4 AND ReservationId IS NOT NULL AND PhysicalDelta < 0 AND PhysicalDelta = ReservedDelta AND QuarantineDelta = 0))");
        });
    }
}
