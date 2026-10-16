using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpticaSosa.Domain.Customers;
using OpticaSosa.Infrastructure.Identity;

namespace OpticaSosa.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.FirstName).HasMaxLength(80).IsRequired();
        entity.Property(x => x.LastName).HasMaxLength(80).IsRequired();
        entity.Property(x => x.Document).HasMaxLength(32).IsRequired();
        entity.Property(x => x.DocumentKey).HasMaxLength(32).IsRequired();
        entity.Property(x => x.SearchName).HasMaxLength(161).IsRequired();
        entity.Property(x => x.Phone).HasMaxLength(40);
        entity.Property(x => x.PhoneKey).HasMaxLength(40);
        entity.Property(x => x.Email).HasMaxLength(254);
        entity.Property(x => x.Version).IsConcurrencyToken();
        entity.HasIndex(x => x.DocumentKey).IsUnique();
        entity.HasIndex(x => x.SearchName);
        entity.HasIndex(x => x.PhoneKey);
        entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
