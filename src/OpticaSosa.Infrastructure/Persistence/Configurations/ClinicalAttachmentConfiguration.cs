using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpticaSosa.Domain.Clinical;
using OpticaSosa.Infrastructure.Identity;

namespace OpticaSosa.Infrastructure.Persistence.Configurations;

public sealed class ClinicalAttachmentConfiguration : IEntityTypeConfiguration<ClinicalAttachment>
{
    public void Configure(EntityTypeBuilder<ClinicalAttachment> e)
    {
        e.HasKey(x => x.Id);
        e.Property(x => x.Version).IsConcurrencyToken();
        e.Property(x => x.OriginalName).HasMaxLength(180).IsRequired();
        e.Property(x => x.ContentType).HasMaxLength(40).IsRequired();
        e.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
        e.Property(x => x.RetirementReason).HasMaxLength(500);
        e.HasOne<ClinicalRevision>().WithMany().HasForeignKey(x => x.RevisionId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RetiredByUserId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.RevisionId, x.CreatedAtUtc, x.Id });
        e.ToTable(t => t.HasCheckConstraint("CK_ClinicalAttachment_State", "State IN (0, 1, 2, 3) AND Length > 0 AND length(Sha256) = 64"));
    }
}

public sealed class ClinicalFilePolicyConfiguration : IEntityTypeConfiguration<ClinicalFilePolicy>
{
    public void Configure(EntityTypeBuilder<ClinicalFilePolicy> e)
    {
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.Version).IsConcurrencyToken();
        e.ToTable(t => t.HasCheckConstraint("CK_ClinicalFilePolicy_Singleton", "Id = 1 AND PhotoMiB > 0 AND PdfMiB > 0"));
    }
}
