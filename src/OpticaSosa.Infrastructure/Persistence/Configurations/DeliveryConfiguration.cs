using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpticaSosa.Domain.Commerce;
using OpticaSosa.Domain.Delivery;
using OpticaSosa.Domain.Inventory;
using OpticaSosa.Domain.Work;
using OpticaSosa.Infrastructure.Identity;

namespace OpticaSosa.Infrastructure.Persistence.Configurations;

public sealed class CommercialWorkConfiguration : IEntityTypeConfiguration<CommercialWork>
{
    public void Configure(EntityTypeBuilder<CommercialWork> e)
    {
        e.HasKey(x => x.WorkId);
        e.HasOne<OpticalWork>().WithMany().HasForeignKey(x => x.WorkId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<SalesOrder>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<CommercialWork>().WithMany().HasForeignKey(x => x.PreviousWorkId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => x.PreviousWorkId).IsUnique(); e.HasIndex(x => new { x.OrderId, x.IsCurrent });
        e.Property(x => x.ReplacementReason).HasMaxLength(500);
        e.ToTable("CommercialWorks", t => t.HasCheckConstraint("CK_CommercialWorks_Profile",
            "IsCurrent IN (0,1) AND (PreviousWorkId IS NULL OR (PreviousWorkId <> WorkId AND length(trim(ReplacementReason)) BETWEEN 1 AND 500)) AND (TechnicalMaterialHash IS NULL OR length(TechnicalMaterialHash)=64) AND (DeliveryMaterialHash IS NULL OR length(DeliveryMaterialHash)=64)"));
    }
}
public sealed class CommercialWorkLineConfiguration : IEntityTypeConfiguration<CommercialWorkLine>
{
    public void Configure(EntityTypeBuilder<CommercialWorkLine> e)
    {
        e.HasKey(x => x.Id);
        e.HasOne<CommercialWork>().WithMany().HasForeignKey(x => x.WorkId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<SalesLine>().WithMany().HasForeignKey(x => x.LineId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<OrderTechnicalAssignment>().WithMany().HasForeignKey(x => x.TechnicalAssignmentId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => x.LineId).IsUnique().HasFilter("IsCurrent = 1");
        e.ToTable("CommercialWorkLines", t => t.HasCheckConstraint("CK_CommercialWorkLines_Current", "IsCurrent IN (0,1)"));
    }
}
public sealed class OrderDeliveryConfiguration : IEntityTypeConfiguration<OrderDelivery>
{
    public void Configure(EntityTypeBuilder<OrderDelivery> e)
    {
        e.HasKey(x => x.Id); e.HasIndex(x => x.OrderId).IsUnique();
        e.HasOne<SalesOrder>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        e.ToTable("OrderDeliveries");
    }
}
