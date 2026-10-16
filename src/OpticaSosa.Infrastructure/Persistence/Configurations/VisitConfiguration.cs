using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpticaSosa.Domain.Customers;
using OpticaSosa.Infrastructure.Identity;

namespace OpticaSosa.Infrastructure.Persistence.Configurations;

public sealed class VisitConfiguration : IEntityTypeConfiguration<Visit>
{
    public void Configure(EntityTypeBuilder<Visit> entity)
    {
        entity.HasKey(x => x.Id);
        entity.HasAlternateKey(x => new { x.CustomerId, x.Id });
        entity.Property(x => x.Reason).HasMaxLength(100).IsRequired();
        entity.Property(x => x.AdministrativeNotes).HasMaxLength(500);
        entity.Property(x => x.Version).IsConcurrencyToken();
        entity.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.EndedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(x => new { x.CustomerId, x.StartedAtUtc });
        entity.HasIndex(x => x.CustomerId).IsUnique().HasFilter("\"Status\" = 0");
        entity.ToTable(table => table.HasCheckConstraint("CK_Visits_Status",
            "(Status = 0 AND EndedAtUtc IS NULL AND EndedByUserId IS NULL) OR " +
            "(Status IN (1, 2) AND EndedAtUtc >= StartedAtUtc AND EndedByUserId IS NOT NULL)"));
    }
}
