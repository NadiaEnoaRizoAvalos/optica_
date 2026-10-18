using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Infrastructure.Identity;

namespace OpticaSosa.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> e)
    {
        e.HasKey(x => x.Id);
        e.HasAlternateKey(x => new { x.Id, x.Category });
        e.Property(x => x.Name).HasMaxLength(160).IsRequired();
        e.Property(x => x.NameKey).HasMaxLength(160).IsRequired();
        e.Property(x => x.Brand).HasMaxLength(100); e.Property(x => x.BrandKey).HasMaxLength(100).IsRequired();
        e.Property(x => x.Model).HasMaxLength(100); e.Property(x => x.ModelKey).HasMaxLength(100).IsRequired();
        e.Property(x => x.Description).HasMaxLength(2000);
        e.Property(x => x.Version).IsConcurrencyToken();
        e.HasIndex(x => new { x.Category, x.NameKey, x.BrandKey, x.ModelKey }).IsUnique();
        e.HasIndex(x => new { x.IsActive, x.NameKey });
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        e.ToTable("Products", t => t.HasCheckConstraint("CK_Products_Profile", "Category BETWEEN 1 AND 4 AND IsActive IN (0,1) AND length(trim(Name)) BETWEEN 1 AND 160"));
    }
}

public sealed class ProductSupplierConfiguration : IEntityTypeConfiguration<ProductSupplier>
{
    public void Configure(EntityTypeBuilder<ProductSupplier> e)
    {
        e.HasKey(x => new { x.ProductId, x.SupplierId });
        e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        e.ToTable("ProductSuppliers", t => t.HasCheckConstraint("CK_ProductSuppliers_Active", "IsActive IN (0,1)"));
    }
}

public sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> e)
    {
        e.HasKey(x => x.Id);
        e.HasOne<Product>().WithMany().HasForeignKey(x => new { x.ProductId, x.Category })
            .HasPrincipalKey(x => new { x.Id, x.Category }).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        e.Property(x => x.Version).IsConcurrencyToken();
        e.Property(x => x.Code).HasMaxLength(80).IsRequired(); e.Property(x => x.CodeKey).HasMaxLength(80).IsRequired();
        e.HasIndex(x => x.CodeKey).IsUnique(); e.HasIndex(x => new { x.ProductId, x.IsActive, x.CodeKey });
        e.Property(x => x.FrameColor).HasMaxLength(80); e.Property(x => x.LensMaterial).HasMaxLength(100);
        e.Property(x => x.LensTreatment).HasMaxLength(160); e.Property(x => x.PriceCurrency).HasMaxLength(3);
        var hundredths = new ValueConverter<decimal, int>(x => decimal.ToInt32(x * 100m), x => x / 100m);
        var thousandths = new ValueConverter<decimal, int>(x => decimal.ToInt32(x * 1000m), x => x / 1000m);
        e.Property(x => x.FrameEyeSizeMm).HasConversion(hundredths).HasColumnName("FrameEyeSizeHundredths");
        e.Property(x => x.FrameBridgeMm).HasConversion(hundredths).HasColumnName("FrameBridgeHundredths");
        e.Property(x => x.FrameTempleMm).HasConversion(hundredths).HasColumnName("FrameTempleHundredths");
        e.Property(x => x.ContactBaseCurveMm).HasConversion(hundredths).HasColumnName("ContactBaseCurveHundredths");
        e.Property(x => x.ContactDiameterMm).HasConversion(hundredths).HasColumnName("ContactDiameterHundredths");
        e.Property(x => x.LensRefractiveIndex).HasConversion(thousandths).HasColumnName("LensIndexThousandths");
        e.Property(x => x.Sphere).HasConversion(thousandths).HasColumnName("SphereMilli");
        e.Property(x => x.Cylinder).HasConversion(thousandths).HasColumnName("CylinderMilli");
        e.Property(x => x.Addition).HasConversion(thousandths).HasColumnName("AdditionMilli");
        e.ToTable("ProductVariants", t =>
        {
            t.HasCheckConstraint("CK_Variants_Unit", "(Category = 3 AND Unit = 2 AND typeof(UnitsPerPackage) = 'integer' AND UnitsPerPackage > 0) OR (Category IN (1,2,4) AND Unit = 1 AND UnitsPerPackage = 1)");
            t.HasCheckConstraint("CK_Variants_CodeState", "length(trim(Code)) BETWEEN 1 AND 80 AND IsActive IN (0,1) AND SpecificationLocked IN (0,1)");
            t.HasCheckConstraint("CK_Variants_Price", "(PriceMinor IS NULL AND PriceCurrency IS NULL) OR (PriceMinor IS NOT NULL AND typeof(PriceMinor) = 'integer' AND PriceMinor >= 0 AND PriceCurrency IS NOT NULL AND PriceCurrency GLOB '[A-Z][A-Z][A-Z]')");
            t.HasCheckConstraint("CK_Variants_Frame", "Category = 1 OR (FrameColor IS NULL AND FrameEyeSizeHundredths IS NULL AND FrameBridgeHundredths IS NULL AND FrameTempleHundredths IS NULL)");
            t.HasCheckConstraint("CK_Variants_Lens", "Category = 2 OR (LensMaterial IS NULL AND LensIndexThousandths IS NULL AND LensTreatment IS NULL)");
            t.HasCheckConstraint("CK_Variants_Powers", "Category IN (2,3) OR (SphereMilli IS NULL AND CylinderMilli IS NULL AND Axis IS NULL AND AdditionMilli IS NULL)");
            t.HasCheckConstraint("CK_Variants_Contact", "Category = 3 OR (ContactBaseCurveHundredths IS NULL AND ContactDiameterHundredths IS NULL)");
            t.HasCheckConstraint("CK_Variants_Axis", "Axis IS NULL OR (typeof(Axis) = 'integer' AND Axis BETWEEN 0 AND 180)");
            foreach (var column in new[] { "FrameEyeSizeHundredths", "FrameBridgeHundredths", "FrameTempleHundredths", "ContactBaseCurveHundredths", "ContactDiameterHundredths", "LensIndexThousandths" })
                t.HasCheckConstraint("CK_Variants_" + column, $"{column} IS NULL OR (typeof({column}) = 'integer' AND {column} > 0 AND {column} <= 2147483647)");
            foreach (var column in new[] { "SphereMilli", "CylinderMilli", "AdditionMilli" })
                t.HasCheckConstraint("CK_Variants_" + column, $"{column} IS NULL OR (typeof({column}) = 'integer' AND {column} BETWEEN -2147483648 AND 2147483647)");
        });
    }
}

public sealed class CommercialSettingsConfiguration : IEntityTypeConfiguration<CommercialSettings>
{
    public void Configure(EntityTypeBuilder<CommercialSettings> e)
    {
        e.HasKey(x => x.Id); e.Property(x => x.Version).IsConcurrencyToken(); e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        e.HasData(new { Id = 1, Currency = "ARS", Version = Guid.Parse("5671154c-b49e-4594-85c9-236d8004fd72") });
        e.ToTable("CommercialSettings", t => t.HasCheckConstraint("CK_CommercialSettings_Currency", "Id = 1 AND Currency GLOB '[A-Z][A-Z][A-Z]'"));
    }
}
