using CodexResetTray.App.Services;

namespace CodexResetTray.Tests;

internal sealed class StubAlertSettingsService : IAlertSettingsService
{
    public StubAlertSettingsService(int? thresholdPercent, bool notificationsEnabled = true)
    {
        LowRemainingThresholdPercent = thresholdPercent;
        NotificationsEnabled = notificationsEnabled;
    }

    public int? LowRemainingThresholdPercent { get; set; }

    public bool NotificationsEnabled { get; set; }
}
