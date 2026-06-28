using CodexResetTray.App.Services;
using CodexResetTray.Core.RateLimits;

namespace CodexResetTray.Tests;

public sealed class RefreshCadenceCalculatorTests
{
    [Fact]
    public void Calculate_returns_five_minutes_when_no_future_reset_exists()
    {
        var now = DateTimeOffset.Parse("2026-06-28T10:00:00Z");
        var snapshot = CreateSnapshot(now, primaryReset: now.AddMinutes(-1), weeklyReset: null);

        var cadence = RefreshCadenceCalculator.Calculate(snapshot, now);

        Assert.Equal(TimeSpan.FromMinutes(5), cadence);
    }

    [Fact]
    public void Calculate_returns_five_minutes_when_nearest_reset_is_not_close()
    {
        var now = DateTimeOffset.Parse("2026-06-28T10:00:00Z");
        var snapshot = CreateSnapshot(now, primaryReset: now.AddMinutes(16), weeklyReset: now.AddDays(2));

        var cadence = RefreshCadenceCalculator.Calculate(snapshot, now);

        Assert.Equal(TimeSpan.FromMinutes(5), cadence);
    }

    [Fact]
    public void Calculate_returns_one_minute_when_nearest_reset_is_under_fifteen_minutes()
    {
        var now = DateTimeOffset.Parse("2026-06-28T10:00:00Z");
        var snapshot = CreateSnapshot(now, primaryReset: now.AddMinutes(15), weeklyReset: now.AddDays(2));

        var cadence = RefreshCadenceCalculator.Calculate(snapshot, now);

        Assert.Equal(TimeSpan.FromMinutes(1), cadence);
    }

    [Fact]
    public void Calculate_returns_thirty_seconds_when_nearest_reset_is_under_two_minutes()
    {
        var now = DateTimeOffset.Parse("2026-06-28T10:00:00Z");
        var snapshot = CreateSnapshot(now, primaryReset: now.AddSeconds(90), weeklyReset: now.AddDays(2));

        var cadence = RefreshCadenceCalculator.Calculate(snapshot, now);

        Assert.Equal(TimeSpan.FromSeconds(30), cadence);
    }

    [Fact]
    public void Calculate_uses_the_nearest_reset_across_all_buckets_and_windows()
    {
        var now = DateTimeOffset.Parse("2026-06-28T10:00:00Z");
        var snapshot = new RateLimitDashboardSnapshot(
            new[]
            {
                new RateLimitBucket(
                    "codex",
                    "Codex",
                    "pro",
                    null,
                    new RateLimitWindowInfo(RateLimitWindowKind.FiveHour, 20, 300, now.AddHours(1)),
                    new RateLimitWindowInfo(RateLimitWindowKind.Weekly, 10, 10080, now.AddDays(2))),
                new RateLimitBucket(
                    "codex_bengalfox",
                    "GPT-5.3-Codex-Spark",
                    "pro",
                    null,
                    new RateLimitWindowInfo(RateLimitWindowKind.FiveHour, 80, 300, now.AddSeconds(75)),
                    null)
            },
            3,
            now);

        var cadence = RefreshCadenceCalculator.Calculate(snapshot, now);

        Assert.Equal(TimeSpan.FromSeconds(30), cadence);
    }

    private static RateLimitDashboardSnapshot CreateSnapshot(
        DateTimeOffset now,
        DateTimeOffset? primaryReset,
        DateTimeOffset? weeklyReset)
    {
        return new RateLimitDashboardSnapshot(
            new[]
            {
                new RateLimitBucket(
                    "codex",
                    "Codex",
                    "pro",
                    null,
                    new RateLimitWindowInfo(RateLimitWindowKind.FiveHour, 20, 300, primaryReset),
                    new RateLimitWindowInfo(RateLimitWindowKind.Weekly, 10, 10080, weeklyReset))
            },
            3,
            now);
    }
}
