using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpticaSosa.Domain.Work;
using OpticaSosa.Domain.Customers;
using OpticaSosa.Domain.Clinical;
using OpticaSosa.Infrastructure.Identity;

namespace OpticaSosa.Infrastructure.Persistence.Configurations;

public sealed class WorkConfiguration : IEntityTypeConfiguration<OpticalWork>
{
    public void Configure(EntityTypeBuilder<OpticalWork> e)
    {
        e.HasKey(x => x.Id); e.Property(x => x.Version).IsConcurrencyToken();
        e.Property(x => x.Product).HasMaxLength(200); e.Property(x => x.Frame).HasMaxLength(200);
        e.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ClinicalRecord>().WithMany().HasForeignKey(x => x.PrescriptionId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ClinicalRevision>().WithMany().HasForeignKey(x => x.PrescriptionRevisionId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.CustomerId, x.CreatedAtUtc });
        e.ToTable(t => {
            t.HasCheckConstraint("CK_OpticalWork_State", "Status BETWEEN 0 AND 5 AND Round > 0 AND Type BETWEEN 0 AND 2 AND (Status NOT IN (4, 5) OR (TechnicalApproved = 1 AND DeliveryApproved = 1))");
            t.HasCheckConstraint("CK_OpticalWork_Prescription", "(PrescriptionId IS NULL) = (PrescriptionRevisionId IS NULL)");
        });
    }
}
public sealed class WorkTransitionConfiguration : IEntityTypeConfiguration<WorkTransition>
{
    public void Configure(EntityTypeBuilder<WorkTransition> e)
    {
        e.HasKey(x => x.Id); e.Property(x => x.Action).HasMaxLength(80);
        e.HasOne<OpticalWork>().WithMany().HasForeignKey(x => x.WorkId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.WorkId, x.AtUtc });
    }
}
public sealed class QualityControlConfiguration : IEntityTypeConfiguration<QualityControl>
{
    public void Configure(EntityTypeBuilder<QualityControl> e)
    {
        e.HasKey(x => x.Id); e.Property(x => x.Notes).HasMaxLength(2000);
        e.HasOne<OpticalWork>().WithMany().HasForeignKey(x => x.WorkId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        e.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.ControlId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.WorkId, x.AtUtc });
        e.ToTable(t => t.HasCheckConstraint("CK_QualityControl_Approval", "Stage IN (0, 1) AND Status IN (0, 1, 2) AND Role IN (0, 1) AND (Stage <> 0 OR Role = 0) AND (Status <> 2 OR ApprovedAtUtc IS NOT NULL)"));
    }
}
public sealed class QualityCheckItemConfiguration : IEntityTypeConfiguration<QualityCheckItem>
{
    public void Configure(EntityTypeBuilder<QualityCheckItem> e)
    {
        e.HasKey(x => new { x.ControlId, x.Key }); e.Property(x => x.Key).HasMaxLength(40);
        e.Property(x => x.Label).HasMaxLength(200); e.Property(x => x.Notes).HasMaxLength(1000);
        e.ToTable(t => t.HasCheckConstraint("CK_QualityCheckItem_Status", "Status BETWEEN 0 AND 3 AND (Status <> 3 OR (Applicable = 0 AND Notes IS NOT NULL AND length(trim(Notes)) > 0))"));
    }
}
public sealed class QualityTemplateConfiguration : IEntityTypeConfiguration<QualityTemplate>
{
    public void Configure(EntityTypeBuilder<QualityTemplate> e)
    {
        e.HasKey(x => new { x.Type, x.Stage }); e.Property(x => x.Version).IsConcurrencyToken();
        e.HasMany(x => x.Items).WithOne().HasForeignKey(x => new { x.Type, x.Stage }).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class QualityTemplateItemConfiguration : IEntityTypeConfiguration<QualityTemplateItem>
{
    public void Configure(EntityTypeBuilder<QualityTemplateItem> e)
    { e.HasKey(x => new { x.Type, x.Stage, x.Key }); e.Property(x => x.Key).HasMaxLength(40); }
}
