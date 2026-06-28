using CodexResetTray.App.Services;
using CodexResetTray.App.ViewModels;
using CodexResetTray.Core.RateLimits;

namespace CodexResetTray.Tests;

public sealed class DashboardViewModelTests
{
    [Fact]
    public async Task RefreshAsync_displays_remaining_percent_but_keeps_used_percent_for_risk_state()
    {
        var snapshot = CreateSnapshot(primaryUsed: 11, weeklyUsed: 14);
        using var viewModel = new DashboardViewModel(new StubRateLimitSource(snapshot));

        await viewModel.RefreshAsync();

        Assert.Equal("89%", viewModel.PrimaryRemainingValueText);
        Assert.Equal("86%", viewModel.WeeklyRemainingValueText);
        Assert.Equal("89% left", viewModel.PrimaryRemainingText);
        Assert.Equal("86% left", viewModel.WeeklyRemainingText);
        Assert.Equal(89, viewModel.PrimaryRemainingPercent);
        Assert.Equal(86, viewModel.WeeklyRemainingPercent);
        Assert.Equal(11, viewModel.PrimaryUsedPercent);
        Assert.Equal(14, viewModel.WeeklyUsedPercent);
        Assert.Equal(11, viewModel.TrayPrimaryPercent);
        Assert.Contains("5h 89% left", viewModel.TrayStatusText);
        Assert.Contains("W 86% left", viewModel.TrayStatusText);
        Assert.Contains("5-hour: 89% left, resets", viewModel.TrayMenuFiveHourText);
        Assert.Contains("Weekly: 86% left, resets", viewModel.TrayMenuWeeklyText);
        Assert.DoesNotContain("used", viewModel.TrayMenuFiveHourText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("left left", viewModel.TrayStatusText, StringComparison.OrdinalIgnoreCase);

        var bucket = Assert.Single(viewModel.Buckets);
        Assert.Equal("89% left", bucket.Primary.RemainingText);
        Assert.Equal(89, bucket.Primary.RemainingPercent);
        Assert.Equal(11, bucket.Primary.UsedPercent);
    }

    [Fact]
    public void StartWithWindowsEnabled_updates_startup_service_and_status()
    {
        var startup = new StubStartupService(isEnabled: false);
        using var viewModel = new DashboardViewModel(new StubRateLimitSource(CreateSnapshot(0, 0)), startupService: startup);

        Assert.True(viewModel.StartupSettingAvailable);
        Assert.False(viewModel.StartWithWindowsEnabled);
        Assert.Equal("Manual start", viewModel.SettingsStatusText);

        viewModel.StartWithWindowsEnabled = true;

        Assert.True(startup.IsEnabled);
        Assert.True(viewModel.StartWithWindowsEnabled);
        Assert.Equal("Starts with Windows", viewModel.SettingsStatusText);
    }

    private static RateLimitDashboardSnapshot CreateSnapshot(int primaryUsed, int weeklyUsed)
    {
        var now = DateTimeOffset.Now;
        return new RateLimitDashboardSnapshot(
            new[]
            {
                new RateLimitBucket(
                    LimitId: "codex",
                    DisplayName: "Codex",
                    PlanType: "pro",
                    RateLimitReachedType: null,
                    Primary: new RateLimitWindowInfo(RateLimitWindowKind.FiveHour, primaryUsed, 300, now.AddHours(1)),
                    Secondary: new RateLimitWindowInfo(RateLimitWindowKind.Weekly, weeklyUsed, 10080, now.AddDays(3)))
            },
            5,
            now);
    }

    private sealed class StubRateLimitSource : IRateLimitSource
    {
        private readonly RateLimitDashboardSnapshot _snapshot;

        public StubRateLimitSource(RateLimitDashboardSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public Task<RateLimitDashboardSnapshot> ReadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_snapshot);
    }

    private sealed class StubStartupService : IStartupService
    {
        public StubStartupService(bool isEnabled)
        {
            IsEnabled = isEnabled;
        }

        public bool IsEnabled { get; private set; }

        public void SetEnabled(bool isEnabled)
        {
            IsEnabled = isEnabled;
        }
    }
}
