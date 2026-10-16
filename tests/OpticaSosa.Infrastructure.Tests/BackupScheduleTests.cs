using OpticaSosa.Infrastructure.Backups;
using Xunit;

namespace OpticaSosa.Infrastructure.Tests;

public sealed class BackupScheduleTests
{
    [Fact]
    public void FirstUsesLocalZoneAndDoesNotForceAnHour()
    {
        var now = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2026, 10, 20, 16, 45, 0, DateTimeKind.Utc), BackupSchedule.First(now, "13:45", "Argentina Standard Time"));
        Assert.Equal(new DateTime(2026, 10, 21, 11, 0, 0, DateTimeKind.Utc), BackupSchedule.First(now, "11:00", "UTC"));
    }
    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("25:00")] [InlineData("20")]
    public void InvalidOrAbsentTimesCannotActivateASchedule(string? value) => Assert.Throws<ArgumentException>(() => BackupSchedule.ParseTime(value));
    [Fact]
    public void AdvancePreservesLocalHourAcrossDaylightSaving()
    {
        var due = BackupSchedule.First(new DateTime(2026, 3, 7, 0, 0, 0, DateTimeKind.Utc), "10:00", "Eastern Standard Time");
        var next = BackupSchedule.Advance(due, 1, "10:00", "Eastern Standard Time");
        Assert.Equal(23, (next - due).TotalHours);
        Assert.Equal(10, TimeZoneInfo.ConvertTimeFromUtc(next, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time")).Hour);
    }
    [Fact]
    public void TraversalAndNonGuidDeletionAreRejected()
    {
        var root = Path.Combine(Path.GetTempPath(), "ficticio-backup");
        Assert.Throws<IOException>(() => BackupFileSafety.Child(root, "../optica.db"));
        Assert.Throws<IOException>(() => BackupFileSafety.DeleteOwnedCopy(root, "arbitrario"));
    }
}
