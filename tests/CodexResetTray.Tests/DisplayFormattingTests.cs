using CodexResetTray.Core.Display;

namespace CodexResetTray.Tests;

public sealed class DisplayFormattingTests
{
    [Fact]
    public void FormatRelativeTime_uses_compact_day_hour_minute_copy()
    {
        var now = new DateTimeOffset(2026, 6, 27, 12, 0, 0, TimeSpan.FromHours(3));

        Assert.Equal("4h 56m", ResetTimeFormatter.FormatRelative(now.AddHours(4).AddMinutes(56), now));
        Assert.Equal("3d 2h", ResetTimeFormatter.FormatRelative(now.AddDays(3).AddHours(2).AddMinutes(31), now));
        Assert.Equal("ready", ResetTimeFormatter.FormatRelative(now.AddSeconds(-1), now));
    }

    [Fact]
    public void FormatExactTime_uses_local_timezone_and_short_weekday()
    {
        var resetUtc = new DateTimeOffset(2026, 7, 1, 23, 15, 42, TimeSpan.Zero);
        var local = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");

        Assert.Equal("Thu, Jul 2, 02:15", ResetTimeFormatter.FormatExact(resetUtc, local));
    }
}
