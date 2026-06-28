using CodexResetTray.Core.RateLimits;

namespace CodexResetTray.App.Services;

public interface IResetCreditExpirySource
{
    Task<ResetCreditReport> ReadAsync(DateTimeOffset fetchedAt, CancellationToken cancellationToken);
}
