using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpticaSosa.Infrastructure.Identity;

namespace OpticaSosa.Infrastructure.Persistence.Configurations;

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> entity)
    {
        entity.Property(x => x.Action).HasMaxLength(64).IsRequired();
        entity.Property(x => x.ActorId).HasMaxLength(450);
        entity.Property(x => x.TargetId).HasMaxLength(450);
    }
}
