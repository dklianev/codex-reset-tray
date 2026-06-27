namespace CodexResetTray.Core.RateLimits;

public sealed record RateLimitDashboardSnapshot(
    IReadOnlyList<RateLimitBucket> Buckets,
    long? ResetCreditsAvailable,
    DateTimeOffset FetchedAt)
{
    public static RateLimitDashboardSnapshot Empty(DateTimeOffset fetchedAt) =>
        new(Array.Empty<RateLimitBucket>(), null, fetchedAt);
}
