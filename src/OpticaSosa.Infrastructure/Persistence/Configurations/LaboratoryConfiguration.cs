using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Commerce;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Domain.Laboratory;
using OpticaSosa.Infrastructure.Identity;

namespace OpticaSosa.Infrastructure.Persistence.Configurations;

public sealed class LaboratoryOrderConfiguration : IEntityTypeConfiguration<LaboratoryOrder>
{
    public void Configure(EntityTypeBuilder<LaboratoryOrder> e)
    {
        e.HasKey(x => x.Id); e.Property(x => x.Version).IsConcurrencyToken();
        e.Ignore(x => x.Status); e.Ignore(x => x.PendingQuantity); e.Ignore(x => x.UsableReceivedQuantity);
        e.HasOne<SalesLine>().WithMany().HasForeignKey(x => x.LineId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<OrderTechnicalAssignment>().WithMany().HasForeignKey(x => x.TechnicalAssignmentId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AuthorizedByUserId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.SentByUserId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.LineId, x.AuthorizedAtUtc, x.Id });
        e.HasIndex(x => new { x.AuthorizedAtUtc, x.Id }); e.Property(x => x.DispatchReference).HasMaxLength(160);
        e.ToTable("LaboratoryOrders", t =>
        {
            t.HasCheckConstraint("CK_LaboratoryOrders_Counts", "typeof(Quantity) = 'integer' AND Quantity > 0 AND typeof(ReceivedQuantity) = 'integer' AND ReceivedQuantity >= 0 AND typeof(CancelledQuantity) = 'integer' AND CancelledQuantity >= 0 AND ReceivedQuantity + CancelledQuantity <= Quantity AND typeof(QuarantinedQuantity) = 'integer' AND QuarantinedQuantity BETWEEN 0 AND ReceivedQuantity");
            t.HasCheckConstraint("CK_LaboratoryOrders_Dispatch", "(SentAtUtc IS NULL AND SentByUserId IS NULL AND DispatchReference IS NULL AND ReceivedQuantity = 0 AND LastReceivedAtUtc IS NULL) OR (SentAtUtc IS NOT NULL AND SentByUserId IS NOT NULL AND DispatchReference IS NOT NULL AND length(trim(DispatchReference)) BETWEEN 1 AND 160)");
            t.HasCheckConstraint("CK_LaboratoryOrders_ReceiptTime", "(ReceivedQuantity = 0 AND LastReceivedAtUtc IS NULL) OR (ReceivedQuantity > 0 AND LastReceivedAtUtc IS NOT NULL)");
        });
    }
}

public sealed class LaboratoryReceiptConfiguration : IEntityTypeConfiguration<LaboratoryReceipt>
{
    public void Configure(EntityTypeBuilder<LaboratoryReceipt> e)
    {
        e.HasKey(x => x.Id);
        e.HasOne<LaboratoryOrder>().WithMany().HasForeignKey(x => x.LaboratoryOrderId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<StockBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => x.BatchId).IsUnique(); e.HasIndex(x => new { x.LaboratoryOrderId, x.ReceivedAtUtc, x.Id });
        e.Property(x => x.Reason).HasMaxLength(500); e.Property(x => x.Reference).HasMaxLength(160);
        e.ToTable("LaboratoryReceipts", t => t.HasCheckConstraint("CK_LaboratoryReceipts_Profile", "typeof(Quantity) = 'integer' AND Quantity > 0 AND typeof(QuarantinedQuantity) = 'integer' AND QuarantinedQuantity BETWEEN 0 AND Quantity AND length(trim(Reason)) BETWEEN 1 AND 500"));
    }
}

public sealed class LaboratoryChangeConfiguration : IEntityTypeConfiguration<LaboratoryChange>
{
    public void Configure(EntityTypeBuilder<LaboratoryChange> e)
    {
        e.HasKey(x => x.Id);
        e.HasOne<LaboratoryOrder>().WithMany().HasForeignKey(x => x.LaboratoryOrderId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.LaboratoryOrderId, x.OccurredAtUtc, x.Id });
        e.Property(x => x.Reason).HasMaxLength(500); e.Property(x => x.LaboratoryConfirmation).HasMaxLength(500);
        e.ToTable("LaboratoryChanges", t =>
        {
            t.HasCheckConstraint("CK_LaboratoryChanges_Profile", "length(trim(Reason)) BETWEEN 1 AND 500 AND typeof(CancelledQuantity) = 'integer' AND ((Kind = 1 AND CancelledQuantity = 0 AND LaboratoryConfirmation IS NULL) OR (Kind = 2 AND CancelledQuantity > 0 AND PreviousEstimatedDate IS NULL AND NewEstimatedDate IS NULL))");
        });
    }
}
