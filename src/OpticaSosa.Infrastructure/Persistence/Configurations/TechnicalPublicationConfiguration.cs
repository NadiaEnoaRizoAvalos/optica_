using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OpticaSosa.Domain.Catalog;
using OpticaSosa.Domain.Clinical;
using OpticaSosa.Domain.Technical;
using OpticaSosa.Infrastructure.Identity;

namespace OpticaSosa.Infrastructure.Persistence.Configurations;

public sealed class TechnicalPublicationConfiguration : IEntityTypeConfiguration<TechnicalPublication>
{
    private static readonly ValueConverter<decimal, int> Hundredths = new(x => decimal.ToInt32(x * 100m), x => x / 100m);
    private static readonly ValueConverter<decimal, int> Thousandths = new(x => decimal.ToInt32(x * 1000m), x => x / 1000m);

    public void Configure(EntityTypeBuilder<TechnicalPublication> e)
    {
        e.HasKey(x => x.Id); e.Property(x => x.Version).IsConcurrencyToken();
        e.HasOne<ClinicalRevision>().WithMany().HasForeignKey(x => x.SourceRevisionId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.PublishedByUserId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RevokedByUserId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<TechnicalPublication>().WithMany().HasForeignKey(x => x.PreviousId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<TechnicalPublication>().WithMany().HasForeignKey(x => x.SupersededById).OnDelete(DeleteBehavior.Restrict);
        e.Property(x => x.CorrectionReason).HasMaxLength(500); e.Property(x => x.RevocationReason).HasMaxLength(500);
        e.HasIndex(x => new { x.SourceRevisionId, x.PublishedAtUtc, x.Id });
        e.HasIndex(x => x.PreviousId).IsUnique();
        e.OwnsOne(x => x.Request, r =>
        {
            r.Property(x => x.Purpose).HasColumnName("Purpose"); r.Property(x => x.RequestedCategory).HasColumnName("RequestedCategory");
            r.Property(x => x.IncludeRight).HasColumnName("IncludeRight"); r.Property(x => x.IncludeLeft).HasColumnName("IncludeLeft");
            r.Property(x => x.Product).HasColumnName("RequestedProduct").HasMaxLength(200).IsRequired();
            r.Property(x => x.ProductId).HasColumnName("ProductId"); r.Property(x => x.Material).HasColumnName("Material").HasMaxLength(100);
            r.Property(x => x.Treatment).HasColumnName("Treatment").HasMaxLength(160);
            r.Property(x => x.RefractiveIndex).HasConversion(Thousandths).HasColumnName("IndexThousandths");
            r.HasOne<Product>().WithMany().HasForeignKey(x => new { x.ProductId, x.RequestedCategory })
                .HasPrincipalKey(x => new { x.Id, x.Category }).OnDelete(DeleteBehavior.Restrict);
            r.OwnsOne(x => x.Mounting, m =>
            {
                m.Property(x => x.RightHeightMm).HasConversion(Hundredths).HasColumnName("RightHeightHundredths");
                m.Property(x => x.LeftHeightMm).HasConversion(Hundredths).HasColumnName("LeftHeightHundredths");
                m.Property(x => x.RightMonocularDistanceMm).HasConversion(Hundredths).HasColumnName("RightMonocularHundredths");
                m.Property(x => x.LeftMonocularDistanceMm).HasConversion(Hundredths).HasColumnName("LeftMonocularHundredths");
                m.Property(x => x.FrameVariantId).HasColumnName("FrameVariantId");
                m.Property(x => x.FrameReference).HasColumnName("FrameReference").HasMaxLength(200);
                m.Property(x => x.Instructions).HasColumnName("MountingInstructions").HasMaxLength(1000);
                m.HasOne<ProductVariant>().WithMany().HasForeignKey(x => x.FrameVariantId).OnDelete(DeleteBehavior.Restrict);
            });
            r.Navigation(x => x.Mounting).IsRequired();
            r.OwnsOne(x => x.RightContact, c => Contact(c, "Right"));
            r.OwnsOne(x => x.LeftContact, c => Contact(c, "Left"));
        });
        e.Navigation(x => x.Request).IsRequired();
        e.OwnsOne(x => x.Right, p => Eye(p, "Right")); e.Navigation(x => x.Right).IsRequired();
        e.OwnsOne(x => x.Left, p => Eye(p, "Left")); e.Navigation(x => x.Left).IsRequired();
        e.ToTable("TechnicalPublications", t =>
        {
            t.HasCheckConstraint("CK_TechnicalPublication_Purpose", "Purpose IN (1,2) AND RequestedCategory = Purpose + 1 AND IncludeRight IN (0,1) AND IncludeLeft IN (0,1) AND (IncludeRight = 1 OR IncludeLeft = 1)");
            t.HasCheckConstraint("CK_TechnicalPublication_Correction", "(PreviousId IS NULL OR (PreviousId <> Id AND CorrectionReason IS NOT NULL AND length(trim(CorrectionReason)) > 0)) AND (SupersededById IS NULL OR SupersededById <> Id)");
            t.HasCheckConstraint("CK_TechnicalPublication_Revocation", "(RevokedAtUtc IS NULL AND RevokedByUserId IS NULL AND RevocationReason IS NULL) OR (RevokedAtUtc IS NOT NULL AND RevokedByUserId IS NOT NULL AND RevocationReason IS NOT NULL AND length(trim(RevocationReason)) > 0)");
            foreach (var side in new[] { "Right", "Left" })
            {
                t.HasCheckConstraint($"CK_TechnicalPublication_{side}Eye", $"(Include{side} = 1 AND {side}SphereMilli IS NOT NULL AND ({side}CylinderMilli IS NULL OR {side}CylinderMilli = 0 OR {side}Axis IS NOT NULL) AND ({side}PrismMilli IS NULL OR {side}PrismMilli = 0 OR ({side}Base IS NOT NULL AND length(trim({side}Base)) > 0))) OR (Include{side} = 0 AND {side}SphereMilli IS NULL AND {side}CylinderMilli IS NULL AND {side}Axis IS NULL AND {side}AdditionMilli IS NULL AND {side}PrismMilli IS NULL AND {side}Base IS NULL AND {side}HeightHundredths IS NULL AND {side}MonocularHundredths IS NULL)");
                t.HasCheckConstraint($"CK_TechnicalPublication_{side}Axis", $"{side}Axis IS NULL OR (typeof({side}Axis) = 'integer' AND {side}Axis BETWEEN 0 AND 180)");
                t.HasCheckConstraint($"CK_TechnicalPublication_{side}Contact", $"(Purpose = 2 AND Include{side} = 1 AND {side}ContactVariantId IS NOT NULL AND {side}ContactBaseCurveHundredths IS NOT NULL AND {side}ContactDiameterHundredths IS NOT NULL AND {side}ContactBaseCurveHundredths > 0 AND {side}ContactDiameterHundredths > 0) OR ((Purpose = 1 OR Include{side} = 0) AND {side}ContactVariantId IS NULL AND {side}ContactBaseCurveHundredths IS NULL AND {side}ContactDiameterHundredths IS NULL)");
                foreach (var field in new[] { "SphereMilli", "CylinderMilli", "AdditionMilli", "PrismMilli" })
                    t.HasCheckConstraint($"CK_TechnicalPublication_{side}{field}", $"{side}{field} IS NULL OR (typeof({side}{field}) = 'integer' AND {side}{field} BETWEEN -9999000 AND 9999000)");
                foreach (var field in new[] { "HeightHundredths", "MonocularHundredths", "ContactBaseCurveHundredths", "ContactDiameterHundredths" })
                    t.HasCheckConstraint($"CK_TechnicalPublication_{side}{field}", $"{side}{field} IS NULL OR (typeof({side}{field}) = 'integer' AND {side}{field} BETWEEN 0 AND 999900)");
            }
            t.HasCheckConstraint("CK_TechnicalPublication_PupillaryDistance", "PupillaryDistanceHundredths IS NULL OR (typeof(PupillaryDistanceHundredths) = 'integer' AND PupillaryDistanceHundredths BETWEEN 0 AND 999900)");
            t.HasCheckConstraint("CK_TechnicalPublication_Index", "IndexThousandths IS NULL OR (typeof(IndexThousandths) = 'integer' AND IndexThousandths BETWEEN 1 AND 9999000)");
            t.HasCheckConstraint("CK_TechnicalPublication_ContactMounting", "Purpose = 1 OR (RightHeightHundredths IS NULL AND LeftHeightHundredths IS NULL AND RightMonocularHundredths IS NULL AND LeftMonocularHundredths IS NULL AND FrameVariantId IS NULL AND FrameReference IS NULL AND MountingInstructions IS NULL AND PupillaryDistanceHundredths IS NULL)");
        });
    }

    private static void Eye(OwnedNavigationBuilder<TechnicalPublication, EyeValues> p, string side)
    {
        p.Property(x => x.Sphere).HasConversion(Thousandths).HasColumnName(side + "SphereMilli");
        p.Property(x => x.Cylinder).HasConversion(Thousandths).HasColumnName(side + "CylinderMilli");
        p.Property(x => x.Addition).HasConversion(Thousandths).HasColumnName(side + "AdditionMilli");
        p.Property(x => x.Prism).HasConversion(Thousandths).HasColumnName(side + "PrismMilli");
        p.Property(x => x.Axis).HasColumnName(side + "Axis"); p.Property(x => x.Base).HasColumnName(side + "Base").HasMaxLength(60);
    }
    private static void Contact(OwnedNavigationBuilder<TechnicalRequest, ContactSelection> c, string side)
    {
        c.Property(x => x.VariantId).HasColumnName(side + "ContactVariantId");
        c.Property(x => x.BaseCurveMm).HasConversion(Hundredths).HasColumnName(side + "ContactBaseCurveHundredths");
        c.Property(x => x.DiameterMm).HasConversion(Hundredths).HasColumnName(side + "ContactDiameterHundredths");
        c.HasOne<ProductVariant>().WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
    }
}
