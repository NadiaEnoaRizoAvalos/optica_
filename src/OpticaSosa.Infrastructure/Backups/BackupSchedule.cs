using System.Globalization;

namespace OpticaSosa.Infrastructure.Backups;

public static class BackupSchedule
{
    public static TimeOnly ParseTime(string? value) => TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture,
        DateTimeStyles.None, out var time) ? time : throw new ArgumentException("Elegí un horario válido (HH:mm).");

    public static DateTime First(DateTime utc, string localTime, string zoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);
        var date = DateOnly.FromDateTime(local);
        var candidate = Convert(date, ParseTime(localTime), zone);
        return candidate >= utc ? candidate : Convert(date.AddDays(1), ParseTime(localTime), zone);
    }

    public static DateTime Advance(DateTime dueUtc, int days, string localTime, string zoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(dueUtc, DateTimeKind.Utc), zone));
        return Convert(date.AddDays(days), ParseTime(localTime), zone);
    }

    private static DateTime Convert(DateOnly date, TimeOnly time, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(local)) local = local.AddMinutes(1);
        // En una hora repetida se elige el primer instante, persistido en UTC una sola vez.
        if (zone.IsAmbiguousTime(local)) return new DateTimeOffset(local, zone.GetAmbiguousTimeOffsets(local).Max()).UtcDateTime;
        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }
}
