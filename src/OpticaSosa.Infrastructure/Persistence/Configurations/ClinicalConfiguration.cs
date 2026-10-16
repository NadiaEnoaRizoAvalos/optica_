using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpticaSosa.Domain.Clinical;
using OpticaSosa.Domain.Customers;
using OpticaSosa.Infrastructure.Identity;

namespace OpticaSosa.Infrastructure.Persistence.Configurations;

public sealed class ClinicalRecordConfiguration : IEntityTypeConfiguration<ClinicalRecord>
{
    public void Configure(EntityTypeBuilder<ClinicalRecord> e)
    {
        e.HasKey(x => x.Id);
        e.Property(x => x.Version).IsConcurrencyToken();
        e.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Visit>().WithMany().HasForeignKey(x => new { x.CustomerId, x.VisitId })
            .HasPrincipalKey(x => new { x.CustomerId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.CustomerId, x.Id });
        e.ToTable(t => t.HasCheckConstraint("CK_ClinicalRecord_Kind", "Kind IN (0, 1, 2) AND RevisionNumber > 0"));
    }
}

public sealed class ClinicalRevisionConfiguration : IEntityTypeConfiguration<ClinicalRevision>
{
    public void Configure(EntityTypeBuilder<ClinicalRevision> e)
    {
        e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.RecordId, x.Number }).IsUnique();
        e.HasOne<ClinicalRecord>().WithMany().HasForeignKey(x => x.RecordId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Professional>().WithMany().HasForeignKey(x => x.ProfessionalId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        e.HasMany(x => x.Eyes).WithOne().HasForeignKey(x => x.RevisionId).OnDelete(DeleteBehavior.Restrict);
        e.Property(x => x.Source).HasMaxLength(200).IsRequired();
        e.Property(x => x.Notes).HasMaxLength(4000);
        e.Property(x => x.ReportedSymptoms).HasMaxLength(2000);
        e.Property(x => x.CorrectionReason).HasMaxLength(500);
        e.Property(x => x.ProfessionalName).HasMaxLength(120).IsRequired();
        e.Property(x => x.ProfessionalRegistration).HasMaxLength(60);
        e.Property(x => x.ProfessionalJurisdiction).HasMaxLength(80);
        e.ToTable(t => t.HasCheckConstraint("CK_ClinicalRevision_Correction", "Number = 1 OR (Number > 1 AND CorrectionReason IS NOT NULL AND length(trim(CorrectionReason)) > 0)"));
    }
}

public sealed class EyeMeasurementConfiguration : IEntityTypeConfiguration<EyeMeasurement>
{
    public void Configure(EntityTypeBuilder<EyeMeasurement> e)
    {
        e.HasKey(x => new { x.RevisionId, x.Side });
        e.Property(x => x.Base).HasMaxLength(60);
        e.ToTable(t => t.HasCheckConstraint("CK_EyeMeasurement_SideAxis", "Side IN (0, 1) AND (Axis IS NULL OR Axis BETWEEN 0 AND 180)"));
    }
}
