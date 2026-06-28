namespace CodexResetTray.Core.RateLimits;

public sealed record RateLimitDashboardSnapshot(
    IReadOnlyList<RateLimitBucket> Buckets,
    long? ResetCreditsAvailable,
    DateTimeOffset FetchedAt,
    ResetCreditReport? ResetCreditDetails = null)
{
    public static RateLimitDashboardSnapshot Empty(DateTimeOffset fetchedAt) =>
        new(Array.Empty<RateLimitBucket>(), null, fetchedAt);
}
