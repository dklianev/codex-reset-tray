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
    }

    [Fact]
    public void Notifications_enabled_persists_false_and_preserves_threshold_changes()
    {
        var path = CreateSettingsPath();
        var service = new JsonAlertSettingsService(path)
        {
            NotificationsEnabled = false,
            LowRemainingThresholdPercent = 20
        };

        var reloaded = new JsonAlertSettingsService(path);

        Assert.False(reloaded.NotificationsEnabled);
        Assert.Equal(20, reloaded.LowRemainingThresholdPercent);
    }

    private static string CreateSettingsPath() =>
        Path.Combine(
            Path.GetTempPath(),
            "CodexResetTrayTests",
            Guid.NewGuid().ToString("N"),
            "settings.json");
}
