using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpticaSosa.Domain.Backups;

namespace OpticaSosa.Infrastructure.Persistence.Configurations;

public sealed class BackupSettingsConfiguration : IEntityTypeConfiguration<BackupSettings>
{
    public void Configure(EntityTypeBuilder<BackupSettings> b)
    {
        b.ToTable("BackupSettings", t =>
        {
            t.HasCheckConstraint("CK_BackupSettings_Singleton", "Id = 1");
            t.HasCheckConstraint("CK_BackupSettings_Values", "EveryDays BETWEEN 1 AND 365 AND KeepCopies BETWEEN 1 AND 3650");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Version).IsConcurrencyToken();
        b.Property(x => x.TimeZoneId).HasMaxLength(128);
        b.Property(x => x.VolumeId).HasMaxLength(512);
        b.Property(x => x.LocalTime).HasMaxLength(5);
    }
}

public sealed class BackupRunConfiguration : IEntityTypeConfiguration<BackupRun>
{
    public void Configure(EntityTypeBuilder<BackupRun> b)
    {
        b.HasKey(x => x.Id); b.HasIndex(x => x.StartedUtc);
        b.Property(x => x.Status).HasMaxLength(32);
        b.Property(x => x.Trigger).HasMaxLength(32);
        b.Property(x => x.Error).HasMaxLength(512);
        b.Property(x => x.VolumeId).HasMaxLength(512);
        b.Property(x => x.DirectoryName).HasMaxLength(32);
        b.Property(x => x.ActorId).HasMaxLength(450);
    }
}
