namespace CodexResetTray.Core.RateLimits;

public sealed record RateLimitBucket(
    string LimitId,
    string DisplayName,
    string? PlanType,
    string? RateLimitReachedType,
    RateLimitWindowInfo? Primary,
    RateLimitWindowInfo? Secondary)
{
    public string StateLabel
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(RateLimitReachedType))
            {
                return "Limited";
            }

            var maxUsed = new[] { Primary?.UsedPercent, Secondary?.UsedPercent }
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .DefaultIfEmpty(0)
                .Max();

            return maxUsed switch
            {
                >= 90 => "Near limit",
                >= 70 => "Watch",
                _ => "Fresh"
            };
        }
    }
}
