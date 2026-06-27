using System.Globalization;

namespace CodexResetTray.Core.Display;

public static class ResetTimeFormatter
{
    public static string FormatRelative(DateTimeOffset? resetAt, DateTimeOffset now)
    {
        if (resetAt is null)
        {
            return "unknown";
        }

        var remaining = resetAt.Value - now;
        if (remaining <= TimeSpan.Zero)
        {
            return "ready";
        }

        if (remaining.TotalDays >= 1)
        {
            return $"{(int)remaining.TotalDays}d {remaining.Hours}h";
        }

        if (remaining.TotalHours >= 1)
        {
            return $"{(int)remaining.TotalHours}h {remaining.Minutes}m";
        }

        return $"{Math.Max(1, remaining.Minutes)}m";
    }

    public static string FormatExact(DateTimeOffset? resetAt, TimeZoneInfo timeZone)
    {
        if (resetAt is null)
        {
            return "Unknown";
        }

        var local = TimeZoneInfo.ConvertTime(resetAt.Value, timeZone);
        return local.ToString("ddd, MMM d, HH:mm", CultureInfo.InvariantCulture);
    }
}
