namespace OpticaSosa.Domain.Backups;

public sealed class BackupSettings
{
    public int Id { get; set; } = 1;
    public Guid InstallationId { get; set; } = Guid.NewGuid();
    public bool Enabled { get; set; }
    public int EveryDays { get; set; } = 1;
    public string? LocalTime { get; set; }
    public string TimeZoneId { get; set; } = TimeZoneInfo.Local.Id;
    public string? VolumeId { get; set; }
    public int KeepCopies { get; set; } = 30;
    public Guid Version { get; set; } = Guid.NewGuid();
    public DateTime? NextUtc { get; set; }
}

public sealed class BackupRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime StartedUtc { get; set; }
    public DateTime? FinishedUtc { get; set; }
    public DateTime? DueUtc { get; set; }
    public DateTime? RetryUtc { get; set; }
    public string Trigger { get; set; } = "Manual";
    public string Status { get; set; } = "Queued";
    public string? Error { get; set; }
    public string? DirectoryName { get; set; }
    public string? VolumeId { get; set; }
    public int Files { get; set; }
    public int MissedPeriods { get; set; }
    public string? ActorId { get; set; }
}
