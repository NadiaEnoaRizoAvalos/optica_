using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Infrastructure.Identity;

namespace OpticaSosa.Infrastructure.Persistence.Configurations;

public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> entity)
    {
        entity.ToTable("Suppliers", t =>
        {
            t.HasCheckConstraint("CK_Suppliers_Name", "length(trim(Name)) BETWEEN 1 AND 160");
            t.HasCheckConstraint("CK_Suppliers_Kind", "SuppliesProducts IN (0,1) AND IsLaboratory IN (0,1) AND (SuppliesProducts = 1 OR IsLaboratory = 1)");
            t.HasCheckConstraint("CK_Suppliers_Active", "IsActive IN (0,1)");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
        entity.Property(x => x.NameKey).HasMaxLength(160).IsRequired();
        entity.Property(x => x.Phone).HasMaxLength(40);
        entity.Property(x => x.Email).HasMaxLength(254);
        entity.Property(x => x.TaxId).HasMaxLength(60);
        entity.Property(x => x.TaxKey).HasMaxLength(60);
        entity.Property(x => x.Services).HasMaxLength(1000);
        entity.Property(x => x.Notes).HasMaxLength(2000);
        entity.Property(x => x.Version).IsConcurrencyToken();
        entity.HasIndex(x => x.NameKey).IsUnique();
        entity.HasIndex(x => x.TaxKey).IsUnique();
        entity.HasIndex(x => new { x.IsActive, x.IsLaboratory, x.NameKey });
        entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
