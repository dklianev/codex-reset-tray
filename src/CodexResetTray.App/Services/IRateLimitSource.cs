using CodexResetTray.Core.RateLimits;

namespace CodexResetTray.App.Services;

public interface IRateLimitSource
{
    Task<RateLimitDashboardSnapshot> ReadAsync(CancellationToken cancellationToken);
}
