namespace CodexResetTray.Core.RateLimits;

public sealed record ResetCreditReport(
    long? AvailableCount,
    IReadOnlyList<ResetCreditInfo> Credits,
    DateTimeOffset FetchedAt)
{
    public ResetCreditInfo? NextExpiringCredit(DateTimeOffset now) =>
        Credits
            .Where(credit => credit.ExpiresAt > now)
            .OrderBy(credit => credit.ExpiresAt)
            .FirstOrDefault();
}
