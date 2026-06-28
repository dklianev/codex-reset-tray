using CodexResetTray.App.Services;
using CodexResetTray.App.ViewModels;
using CodexResetTray.Core.Display;
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
        Assert.Contains("5-hour: 89% left | resets in", viewModel.TrayMenuFiveHourText);
        Assert.Contains("Weekly: 86% left | resets in", viewModel.TrayMenuWeeklyText);
        Assert.DoesNotContain("used", viewModel.TrayMenuFiveHourText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("left left", viewModel.TrayStatusText, StringComparison.OrdinalIgnoreCase);

        var bucket = Assert.Single(viewModel.Buckets);
        Assert.Equal("89% left", bucket.Primary.RemainingText);
        Assert.Equal(89, bucket.Primary.RemainingPercent);
        Assert.Equal(11, bucket.Primary.UsedPercent);
    }

    [Fact]
    public async Task RefreshAsync_builds_compact_tooltip_and_menu_with_exact_reset_times()
    {
        var snapshot = CreateSnapshot(primaryUsed: 27, weeklyUsed: 16);
        using var viewModel = new DashboardViewModel(new StubRateLimitSource(snapshot));

        await viewModel.RefreshAsync();

        Assert.StartsWith("Codex | 5h 73% left >", viewModel.TrayTooltip);
        Assert.Contains(" | W 84% left >", viewModel.TrayTooltip);
        Assert.Contains(" | C5", viewModel.TrayTooltip);
        Assert.True(viewModel.TrayTooltip.Length <= 63, viewModel.TrayTooltip);

        Assert.Contains("73% left", viewModel.TrayMenuFiveHourText);
        Assert.Contains("resets", viewModel.TrayMenuFiveHourText);
        Assert.Contains(ResetTimeFormatter.FormatExact(snapshot.Buckets[0].Primary!.ResetsAt, TimeZoneInfo.Local), viewModel.TrayMenuFiveHourText);

        Assert.Contains("84% left", viewModel.TrayMenuWeeklyText);
        Assert.Contains(ResetTimeFormatter.FormatExact(snapshot.Buckets[0].Secondary!.ResetsAt, TimeZoneInfo.Local), viewModel.TrayMenuWeeklyText);
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

    [Fact]
    public void LowRemainingAlertThreshold_updates_settings_service_and_status_text()
    {
        var alerts = new StubAlertSettingsService(thresholdPercent: 15);
        using var viewModel = new DashboardViewModel(
            new StubRateLimitSource(CreateSnapshot(0, 0)),
            alertSettingsService: alerts);

        Assert.Equal(15, viewModel.LowRemainingAlertThresholdPercent);
        Assert.Equal("Low alerts: 15% left", viewModel.LowRemainingAlertThresholdText);

        viewModel.LowRemainingAlertThresholdPercent = null;

        Assert.Null(alerts.LowRemainingThresholdPercent);
        Assert.Equal("Low alerts: off", viewModel.LowRemainingAlertThresholdText);
    }

    [Fact]
    public void NotificationsEnabled_updates_settings_service_and_status_text()
    {
        var alerts = new StubAlertSettingsService(thresholdPercent: 10, notificationsEnabled: false);
        using var viewModel = new DashboardViewModel(
            new StubRateLimitSource(CreateSnapshot(0, 0)),
            alertSettingsService: alerts);

        Assert.False(viewModel.NotificationsEnabled);
        Assert.Equal("Notifications: off", viewModel.NotificationsEnabledText);

        viewModel.NotificationsEnabled = true;

        Assert.True(alerts.NotificationsEnabled);
        Assert.True(viewModel.NotificationsEnabled);
        Assert.Equal("Notifications: on", viewModel.NotificationsEnabledText);
    }

    [Fact]
    public void ResetCreditExpiryLookupEnabled_updates_settings_service_and_status_text()
    {
        var alerts = new StubAlertSettingsService(thresholdPercent: 10);
        using var viewModel = new DashboardViewModel(
            new StubRateLimitSource(CreateSnapshot(0, 0)),
            alertSettingsService: alerts);

        Assert.False(viewModel.ResetCreditExpiryLookupEnabled);
        Assert.Equal("Expiry lookup: off", viewModel.ResetCreditExpiryLookupText);
        Assert.Equal("Credit expiry: lookup off", viewModel.ResetCreditExpiryText);

        viewModel.ResetCreditExpiryLookupEnabled = true;

        Assert.True(alerts.ResetCreditExpiryLookupEnabled);
        Assert.True(viewModel.ResetCreditExpiryLookupEnabled);
        Assert.Equal("Expiry lookup: on", viewModel.ResetCreditExpiryLookupText);
    }

    [Fact]
    public async Task RefreshAsync_displays_reset_credit_expiry_metadata_when_enabled()
    {
        var snapshot = CreateSnapshotWithResetCreditDetails();
        var alerts = new StubAlertSettingsService(thresholdPercent: 10)
        {
            ResetCreditExpiryLookupEnabled = true
        };
        using var viewModel = new DashboardViewModel(
            new StubRateLimitSource(snapshot),
            alertSettingsService: alerts);

        await viewModel.RefreshAsync();

        var nextExpiry = snapshot.ResetCreditDetails!.Credits[0].ExpiresAt;
        Assert.Equal("Credit expiry: 2 tracked", viewModel.ResetCreditExpiryText);
        Assert.Contains("Next expires in", viewModel.ResetCreditExpiryDetailText);
        Assert.Contains(ResetTimeFormatter.FormatExact(nextExpiry, TimeZoneInfo.Local), viewModel.ResetCreditExpiryDetailText);
        Assert.Contains("Reset expiry: 2 tracked", viewModel.TrayMenuCreditExpiryText);
        Assert.Contains(ResetTimeFormatter.FormatExact(nextExpiry, TimeZoneInfo.Local), viewModel.TrayMenuCreditExpiryText);
    }

    [Fact]
    public void SetAutoRefreshCadence_updates_footer_status()
    {
        using var viewModel = new DashboardViewModel(new StubRateLimitSource(CreateSnapshot(0, 0)));

        viewModel.SetAutoRefreshCadence(TimeSpan.FromSeconds(30));

        Assert.Equal("Smart refresh: 30s", viewModel.AutoRefreshText);
    }

    [Fact]
    public async Task RefreshAsync_records_last_successful_snapshot_and_raises_snapshot_applied()
    {
        var snapshot = CreateSnapshot(primaryUsed: 11, weeklyUsed: 14);
        using var viewModel = new DashboardViewModel(new StubRateLimitSource(snapshot));
        var appliedCount = 0;
        viewModel.SnapshotApplied += (_, _) => appliedCount++;

        await viewModel.RefreshAsync();

        Assert.Same(snapshot, viewModel.LastSuccessfulSnapshot);
        Assert.Equal(1, appliedCount);
    }

    [Fact]
    public async Task RefreshAsync_does_not_clear_last_successful_snapshot_after_failure()
    {
        var snapshot = CreateSnapshot(primaryUsed: 11, weeklyUsed: 14);
        using var viewModel = new DashboardViewModel(
            new SequenceRateLimitSource(
                Task.FromResult(snapshot),
                Task.FromException<RateLimitDashboardSnapshot>(new InvalidOperationException("boom"))));

        await viewModel.RefreshAsync();
        await viewModel.RefreshAsync();

        Assert.Same(snapshot, viewModel.LastSuccessfulSnapshot);
        Assert.Contains("boom", viewModel.ErrorText);
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

    private static RateLimitDashboardSnapshot CreateSnapshotWithResetCreditDetails()
    {
        var now = DateTimeOffset.Parse("2026-06-28T10:00:00Z");
        var report = new ResetCreditReport(
            2,
            new[]
            {
                new ResetCreditInfo(
                    "One free rate limit reset",
                    "available",
                    "codex_rate_limits",
                    now.AddDays(-16),
                    now.AddDays(1)),
                new ResetCreditInfo(
                    "One free rate limit reset",
                    "available",
                    "codex_rate_limits",
                    now.AddDays(-10),
                    now.AddDays(10))
            },
            now);

        return new RateLimitDashboardSnapshot(
            new[]
            {
                new RateLimitBucket(
                    LimitId: "codex",
                    DisplayName: "Codex",
                    PlanType: "pro",
                    RateLimitReachedType: null,
                    Primary: new RateLimitWindowInfo(RateLimitWindowKind.FiveHour, 11, 300, now.AddHours(1)),
                    Secondary: new RateLimitWindowInfo(RateLimitWindowKind.Weekly, 14, 10080, now.AddDays(3)))
            },
            5,
            now,
            report);
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

    private sealed class SequenceRateLimitSource : IRateLimitSource
    {
        private readonly Queue<Task<RateLimitDashboardSnapshot>> _reads;

        public SequenceRateLimitSource(params Task<RateLimitDashboardSnapshot>[] reads)
        {
            _reads = new Queue<Task<RateLimitDashboardSnapshot>>(reads);
        }

        public Task<RateLimitDashboardSnapshot> ReadAsync(CancellationToken cancellationToken) =>
            _reads.Dequeue();
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
