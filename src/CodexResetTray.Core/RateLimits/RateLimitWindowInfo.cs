namespace CodexResetTray.Core.RateLimits;

public sealed record RateLimitWindowInfo(
    RateLimitWindowKind Kind,
    int UsedPercent,
    int? WindowDurationMinutes,
    DateTimeOffset? ResetsAt)
{
    public string Label => Kind switch
    {
        RateLimitWindowKind.FiveHour => "5-hour",
        RateLimitWindowKind.Weekly => "Weekly",
        _ => WindowDurationMinutes is { } minutes ? $"{minutes} min" : "Unknown"
    };
}
