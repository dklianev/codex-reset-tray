using CodexResetTray.Core.RateLimits;

namespace CodexResetTray.App.Services;

public sealed class ResetCreditExpiryRateLimitSource : IRateLimitSource, IDisposable
{
    private readonly IRateLimitSource _baseSource;
    private readonly IResetCreditExpirySource _expirySource;
    private readonly IAlertSettingsService _settings;

    public ResetCreditExpiryRateLimitSource(
        IRateLimitSource baseSource,
        IResetCreditExpirySource expirySource,
        IAlertSettingsService settings)
    {
        _baseSource = baseSource;
        _expirySource = expirySource;
        _settings = settings;
    }

    public async Task<RateLimitDashboardSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        var snapshot = await _baseSource.ReadAsync(cancellationToken);
        if (!_settings.ResetCreditExpiryLookupEnabled)
        {
            return snapshot;
        }

        try
        {
            var report = await _expirySource.ReadAsync(snapshot.FetchedAt, cancellationToken);
            return snapshot with { ResetCreditDetails = report };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return snapshot;
        }
    }

    public void Dispose()
    {
        if (_baseSource is IDisposable baseDisposable)
        {
            baseDisposable.Dispose();
        }

        if (_expirySource is IDisposable expiryDisposable)
        {
            expiryDisposable.Dispose();
        }
    }
}
