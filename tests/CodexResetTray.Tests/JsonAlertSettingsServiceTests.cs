using CodexResetTray.App.Services;

namespace CodexResetTray.Tests;

public sealed class JsonAlertSettingsServiceTests
{
    [Fact]
    public void Missing_settings_file_loads_default_alert_settings()
    {
        var path = CreateSettingsPath();
        var service = new JsonAlertSettingsService(path);

        Assert.Equal(10, service.LowRemainingThresholdPercent);
        Assert.True(service.NotificationsEnabled);
        Assert.False(service.ResetCreditExpiryLookupEnabled);
        Assert.Equal(48, service.ResetCreditExpiryWarningHours);
    }

    [Fact]
    public void Old_settings_json_loads_notifications_as_enabled()
    {
        var path = CreateSettingsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{\"LowRemainingThresholdPercent\":15}");

        var service = new JsonAlertSettingsService(path);

        Assert.Equal(15, service.LowRemainingThresholdPercent);
        Assert.True(service.NotificationsEnabled);
        Assert.False(service.ResetCreditExpiryLookupEnabled);
        Assert.Equal(48, service.ResetCreditExpiryWarningHours);
    }

    [Fact]
    public void Notifications_enabled_persists_false_and_preserves_threshold_changes()
    {
        var path = CreateSettingsPath();
        var service = new JsonAlertSettingsService(path)
        {
            NotificationsEnabled = false,
            LowRemainingThresholdPercent = 20,
            ResetCreditExpiryLookupEnabled = true,
            ResetCreditExpiryWarningHours = 12
        };

        var reloaded = new JsonAlertSettingsService(path);

        Assert.False(reloaded.NotificationsEnabled);
        Assert.Equal(20, reloaded.LowRemainingThresholdPercent);
        Assert.True(reloaded.ResetCreditExpiryLookupEnabled);
        Assert.Equal(12, reloaded.ResetCreditExpiryWarningHours);
    }

    [Fact]
    public void Reset_credit_expiry_warning_hours_are_sanitized()
    {
        var path = CreateSettingsPath();
        var service = new JsonAlertSettingsService(path)
        {
            ResetCreditExpiryWarningHours = 0
        };

        Assert.Equal(1, service.ResetCreditExpiryWarningHours);

        service.ResetCreditExpiryWarningHours = 999;

        Assert.Equal(720, service.ResetCreditExpiryWarningHours);
    }

    private static string CreateSettingsPath() =>
        Path.Combine(
            Path.GetTempPath(),
            "CodexResetTrayTests",
            Guid.NewGuid().ToString("N"),
            "settings.json");
}
