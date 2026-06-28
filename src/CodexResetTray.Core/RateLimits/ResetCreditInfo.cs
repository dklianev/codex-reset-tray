namespace CodexResetTray.Core.RateLimits;

public sealed record ResetCreditInfo(
    string Title,
    string Status,
    string ResetType,
    DateTimeOffset GrantedAt,
    DateTimeOffset ExpiresAt);
