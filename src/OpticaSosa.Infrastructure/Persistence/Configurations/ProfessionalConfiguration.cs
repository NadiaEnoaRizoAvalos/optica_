using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpticaSosa.Domain.Clinical;
using OpticaSosa.Infrastructure.Identity;

namespace OpticaSosa.Infrastructure.Persistence.Configurations;

public sealed class ProfessionalConfiguration : IEntityTypeConfiguration<Professional>
{
    public void Configure(EntityTypeBuilder<Professional> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.FullName).HasMaxLength(120).IsRequired();
        entity.Property(x => x.Registration).HasMaxLength(60);
        entity.Property(x => x.Jurisdiction).HasMaxLength(80);
        entity.Property(x => x.DuplicateKey).HasMaxLength(262).IsRequired();
        entity.Property(x => x.Version).IsConcurrencyToken();
        entity.HasIndex(x => x.DuplicateKey).IsUnique();
        entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
