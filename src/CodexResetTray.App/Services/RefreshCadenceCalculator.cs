using CodexResetTray.Core.RateLimits;

namespace CodexResetTray.App.Services;

public static class RefreshCadenceCalculator
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan NearResetInterval = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan ImminentResetInterval = TimeSpan.FromSeconds(30);

    public static TimeSpan Calculate(RateLimitDashboardSnapshot? snapshot, DateTimeOffset now)
    {
        if (snapshot is null)
        {
            return DefaultInterval;
        }

        var nearestReset = snapshot.Buckets
            .SelectMany(bucket => new[] { bucket.Primary?.ResetsAt, bucket.Secondary?.ResetsAt })
            .Where(resetAt => resetAt.HasValue && resetAt.Value > now)
            .Select(resetAt => resetAt!.Value)
            .OrderBy(resetAt => resetAt)
            .FirstOrDefault();

        if (nearestReset == default)
        {
            return DefaultInterval;
        }

        var remaining = nearestReset - now;
        if (remaining <= TimeSpan.FromMinutes(2))
        {
            return ImminentResetInterval;
        }

        if (remaining <= TimeSpan.FromMinutes(15))
        {
            return NearResetInterval;
        }

        return DefaultInterval;
    }
}
