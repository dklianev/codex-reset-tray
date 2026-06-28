using CodexResetTray.App.Services;

namespace CodexResetTray.Tests;

internal sealed class StubAlertSettingsService : IAlertSettingsService
{
    public StubAlertSettingsService(int? thresholdPercent)
    {
        LowRemainingThresholdPercent = thresholdPercent;
    }

    public int? LowRemainingThresholdPercent { get; set; }
}
