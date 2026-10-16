using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpticaSosa.Domain.Business;

namespace OpticaSosa.Infrastructure.Persistence.Configurations;

public sealed class BusinessSettingsConfiguration : IEntityTypeConfiguration<BusinessSettings>
{
    public void Configure(EntityTypeBuilder<BusinessSettings> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.Version).IsConcurrencyToken();
        entity.ToTable(table => table.HasCheckConstraint("CK_BusinessSettings_Singleton", "Id = 1"));
        entity.OwnsOne(x => x.Profile, profile =>
        {
            profile.Property(x => x.TradeName).HasMaxLength(120);
            profile.Property(x => x.Address).HasMaxLength(200);
            profile.Property(x => x.Province).HasMaxLength(80);
            profile.Property(x => x.Locality).HasMaxLength(100);
            profile.Property(x => x.Phone).HasMaxLength(40);
            profile.Property(x => x.WhatsApp).HasMaxLength(40);
            profile.Property(x => x.Email).HasMaxLength(254);
            profile.Property(x => x.Cuit).HasMaxLength(20);
            profile.Property(x => x.AdditionalDetails).HasMaxLength(2000);
        });
        entity.Navigation(x => x.Profile).IsRequired();
    }
}
