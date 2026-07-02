using CodexResetTray.App.Services;
using CodexResetTray.App.ViewModels;
using CodexResetTray.Core.RateLimits;

namespace CodexResetTray.Tests;

public sealed class DashboardNotificationTests
{
    [Fact]
    public async Task RefreshAsync_notifies_when_remaining_crosses_configured_low_threshold_once()
    {
        var now = DateTimeOffset.Now;
        using var viewModel = new DashboardViewModel(
            new SequenceRateLimitSource(
                CreateSnapshot(now, primaryUsed: 80, weeklyUsed: 20, credits: 3),
                CreateSnapshot(now.AddMinutes(1), primaryUsed: 92, weeklyUsed: 20, credits: 3),
                CreateSnapshot(now.AddMinutes(2), primaryUsed: 94, weeklyUsed: 20, credits: 3)),
            alertSettingsService: new StubAlertSettingsService(thresholdPercent: 10));
        var notifications = CaptureNotifications(viewModel);

        await viewModel.RefreshAsync();
        await viewModel.RefreshAsync(isSilent: true);
        await viewModel.RefreshAsync(isSilent: true);

        var notification = Assert.Single(notifications);
        Assert.Equal(TrayNotificationLevel.Warning, notification.Level);
        Assert.Equal("Low Codex capacity", notification.Title);
        Assert.Contains("5-hour 8% left", notification.Text);
        Assert.Equal(1, viewModel.UnreadNotificationCount);
        Assert.True(viewModel.HasUnreadNotifications);
        Assert.Equal("Low Codex capacity", Assert.Single(viewModel.Notifications).Title);
    }

    [Fact]
    public async Task RefreshAsync_does_not_notify_when_first_snapshot_is_already_below_threshold()
    {
        var now = DateTimeOffset.Now;
        using var viewModel = new DashboardViewModel(
            new SequenceRateLimitSource(
                CreateSnapshot(now, primaryUsed: 95, weeklyUsed: 20, credits: 3),
                CreateSnapshot(now.AddMinutes(1), primaryUsed: 96, weeklyUsed: 20, credits: 3)),
            alertSettingsService: new StubAlertSettingsService(thresholdPercent: 10));
        var notifications = CaptureNotifications(viewModel);

        await viewModel.RefreshAsync();
        await viewModel.RefreshAsync(isSilent: true);

        Assert.Empty(notifications);
        Assert.Empty(viewModel.Notifications);
    }

    [Fact]
    public async Task RefreshAsync_rearms_low_remaining_alert_after_recovery()
    {
        var now = DateTimeOffset.Now;
        using var viewModel = new DashboardViewModel(
            new SequenceRateLimitSource(
                CreateSnapshot(now, primaryUsed: 80, weeklyUsed: 20, credits: 3),
                CreateSnapshot(now.AddMinutes(1), primaryUsed: 92, weeklyUsed: 20, credits: 3),
                CreateSnapshot(now.AddMinutes(2), primaryUsed: 70, weeklyUsed: 20, credits: 3),
                CreateSnapshot(now.AddMinutes(3), primaryUsed: 93, weeklyUsed: 20, credits: 3)),
            alertSettingsService: new StubAlertSettingsService(thresholdPercent: 10));
        var notifications = CaptureNotifications(viewModel);

        await viewModel.RefreshAsync();
        await viewModel.RefreshAsync(isSilent: true);
        await viewModel.RefreshAsync(isSilent: true);
        await viewModel.RefreshAsync(isSilent: true);

        Assert.Equal(2, notifications.Count);
        Assert.Equal(2, viewModel.Notifications.Count);
    }

    [Fact]
    public async Task RefreshAsync_notifies_when_manual_reset_credits_are_added()
    {
        var now = DateTimeOffset.Now;
        using var viewModel = new DashboardViewModel(
            new SequenceRateLimitSource(
                CreateSnapshot(now, primaryUsed: 20, weeklyUsed: 30, credits: 2),
                CreateSnapshot(now.AddMinutes(1), primaryUsed: 20, weeklyUsed: 30, credits: 4)),
            alertSettingsService: new StubAlertSettingsService(thresholdPercent: null));
        var notifications = CaptureNotifications(viewModel);

        await viewModel.RefreshAsync();
        await viewModel.RefreshAsync(isSilent: true);

        var notification = Assert.Single(notifications);
        Assert.Equal(TrayNotificationLevel.Info, notification.Level);
        Assert.Equal("Manual reset credits added", notification.Title);
        Assert.Contains("+2", notification.Text);
        Assert.Contains("4 total", notification.Text);
        Assert.Equal("Manual reset credits added", Assert.Single(viewModel.Notifications).Title);
    }

    [Fact]
    public async Task RefreshAsync_notifies_when_limits_are_directly_reset_before_scheduled_reset()
    {
        var now = DateTimeOffset.Now;
        using var viewModel = new DashboardViewModel(
            new SequenceRateLimitSource(
                CreateSnapshot(now, primaryUsed: 96, weeklyUsed: 64, credits: 0, primaryReset: now.AddHours(2)),
                CreateSnapshot(now.AddMinutes(1), primaryUsed: 0, weeklyUsed: 64, credits: 0, primaryReset: now.AddHours(5))),
            alertSettingsService: new StubAlertSettingsService(thresholdPercent: null));
        var notifications = CaptureNotifications(viewModel);

        await viewModel.RefreshAsync();
        await viewModel.RefreshAsync(isSilent: true);

        var notification = Assert.Single(notifications);
        Assert.Equal(TrayNotificationLevel.Info, notification.Level);
        Assert.Equal("Codex limits reset", notification.Title);
        Assert.Contains("5-hour", notification.Text);
        Assert.Contains("100% left", notification.Text);
        Assert.Equal("Codex limits reset", Assert.Single(viewModel.Notifications).Title);
    }

    [Fact]
    public async Task RefreshAsync_notifies_when_limits_are_directly_reset_with_smaller_usage_drop()
    {
        var now = DateTimeOffset.Now;
        using var viewModel = new DashboardViewModel(
            new SequenceRateLimitSource(
                CreateSnapshot(now, primaryUsed: 28, weeklyUsed: 64, credits: 0, primaryReset: now.AddHours(2)),
                CreateSnapshot(now.AddMinutes(1), primaryUsed: 0, weeklyUsed: 64, credits: 0, primaryReset: now.AddHours(5))),
            alertSettingsService: new StubAlertSettingsService(thresholdPercent: null));
        var notifications = CaptureNotifications(viewModel);

        await viewModel.RefreshAsync();
        await viewModel.RefreshAsync(isSilent: true);

        var notification = Assert.Single(notifications);
        Assert.Equal(TrayNotificationLevel.Info, notification.Level);
        Assert.Equal("Codex limits reset", notification.Title);
        Assert.Contains("5-hour", notification.Text);
        Assert.Contains("100% left", notification.Text);
    }

    [Fact]
    public async Task RefreshAsync_notifies_when_reset_credit_is_consumed_for_instant_reset()
    {
        var now = DateTimeOffset.Now;
        using var viewModel = new DashboardViewModel(
            new SequenceRateLimitSource(
                CreateSnapshot(now, primaryUsed: 28, weeklyUsed: 64, credits: 2, primaryReset: now.AddHours(2)),
                CreateSnapshot(now.AddMinutes(1), primaryUsed: 0, weeklyUsed: 64, credits: 1, primaryReset: now.AddHours(5))),
            alertSettingsService: new StubAlertSettingsService(thresholdPercent: null));
        var notifications = CaptureNotifications(viewModel);

        await viewModel.RefreshAsync();
        await viewModel.RefreshAsync(isSilent: true);

        var notification = Assert.Single(notifications);
        Assert.Equal(TrayNotificationLevel.Info, notification.Level);
        Assert.Equal("Reset credit used", notification.Title);
        Assert.Contains("-1 reset credit", notification.Text);
        Assert.Contains("1 remaining", notification.Text);
        Assert.Contains("5-hour 100% left", notification.Text);
    }

    [Fact]
    public async Task RefreshAsync_keeps_reset_info_when_low_alert_fires_on_same_refresh()
    {
        var now = DateTimeOffset.Now;
        using var viewModel = new DashboardViewModel(
            new SequenceRateLimitSource(
                CreateSnapshot(now, primaryUsed: 96, weeklyUsed: 80, credits: 0, primaryReset: now.AddHours(2)),
                CreateSnapshot(now.AddMinutes(1), primaryUsed: 0, weeklyUsed: 92, credits: 0, primaryReset: now.AddHours(5))),
            alertSettingsService: new StubAlertSettingsService(thresholdPercent: 10));
        var notifications = CaptureNotifications(viewModel);

        await viewModel.RefreshAsync();
        await viewModel.RefreshAsync(isSilent: true);

        Assert.Equal(2, notifications.Count);
        Assert.Contains(notifications, notification => notification.Title == "Low Codex capacity");
        Assert.Contains(notifications, notification => notification.Title == "Codex limits reset");
        Assert.Equal(2, viewModel.Notifications.Count);
    }

    [Fact]
    public async Task RefreshAsync_notifies_when_tracked_reset_credit_is_added_without_count_change()
    {
        var now = DateTimeOffset.Parse("2026-06-28T10:00:00Z");
        var alerts = new StubAlertSettingsService(thresholdPercent: null)
        {
            ResetCreditExpiryLookupEnabled = true,
            ResetCreditExpiryWarningHours = 48
        };
        var firstCreditExpires = now.AddDays(20);
        var secondCreditExpires = now.AddDays(22);
        using var viewModel = new DashboardViewModel(
            new SequenceRateLimitSource(
                CreateSnapshot(now, primaryUsed: 20, weeklyUsed: 30, credits: 2, resetCreditDetails: CreateCreditReport(now, firstCreditExpires)),
                CreateSnapshot(now.AddMinutes(1), primaryUsed: 20, weeklyUsed: 30, credits: 2, resetCreditDetails: CreateCreditReport(now.AddMinutes(1), firstCreditExpires, secondCreditExpires))),
            alertSettingsService: alerts);
        var notifications = CaptureNotifications(viewModel);

        await viewModel.RefreshAsync();
        await viewModel.RefreshAsync(isSilent: true);

        var notification = Assert.Single(notifications);
        Assert.Equal(TrayNotificationLevel.Info, notification.Level);
        Assert.Equal("Reset credits added", notification.Title);
        Assert.Contains("+1 tracked", notification.Text);
        Assert.Contains("2 tracked", notification.Text);
    }

    [Fact]
    public async Task RefreshAsync_does_not_notify_when_expiry_lookup_first_discovers_existing_credits()
    {
        var now = DateTimeOffset.Parse("2026-06-28T10:00:00Z");
        var alerts = new StubAlertSettingsService(thresholdPercent: null)
        {
            ResetCreditExpiryLookupEnabled = true,
            ResetCreditExpiryWarningHours = 48
        };
        var firstCreditExpires = now.AddDays(20);
        var secondCreditExpires = now.AddDays(22);
        using var viewModel = new DashboardViewModel(
            new SequenceRateLimitSource(
                CreateSnapshot(now, primaryUsed: 20, weeklyUsed: 30, credits: 2),
                CreateSnapshot(now.AddMinutes(1), primaryUsed: 20, weeklyUsed: 30, credits: 2, resetCreditDetails: CreateCreditReport(now.AddMinutes(1), firstCreditExpires, secondCreditExpires))),
            alertSettingsService: alerts);
        var notifications = CaptureNotifications(viewModel);

        await viewModel.RefreshAsync();
        await viewModel.RefreshAsync(isSilent: true);

        Assert.Empty(notifications);
        Assert.Empty(viewModel.Notifications);
    }

    [Fact]
    public async Task RefreshAsync_does_not_notify_for_natural_window_reset_after_scheduled_time()
    {
        var now = DateTimeOffset.Now;
        using var viewModel = new DashboardViewModel(
            new SequenceRateLimitSource(
                CreateSnapshot(now, primaryUsed: 96, weeklyUsed: 64, credits: 0, primaryReset: now.AddSeconds(-5)),
                CreateSnapshot(now.AddMinutes(1), primaryUsed: 0, weeklyUsed: 64, credits: 0, primaryReset: now.AddHours(5))),
            alertSettingsService: new StubAlertSettingsService(thresholdPercent: null));
        var notifications = CaptureNotifications(viewModel);

        await viewModel.RefreshAsync();
        await viewModel.RefreshAsync(isSilent: true);

        Assert.Empty(notifications);
        Assert.Empty(viewModel.Notifications);
    }

    [Fact]
    public async Task RefreshAsync_does_not_record_or_raise_notifications_when_notifications_are_disabled()
    {
        var now = DateTimeOffset.Now;
        using var viewModel = new DashboardViewModel(
            new SequenceRateLimitSource(
                CreateSnapshot(now, primaryUsed: 80, weeklyUsed: 30, credits: 2),
                CreateSnapshot(now.AddMinutes(1), primaryUsed: 92, weeklyUsed: 30, credits: 4),
                CreateSnapshot(now.AddMinutes(2), primaryUsed: 0, weeklyUsed: 30, credits: 4, primaryReset: now.AddHours(5))),
            alertSettingsService: new StubAlertSettingsService(thresholdPercent: 10, notificationsEnabled: false));
        var notifications = CaptureNotifications(viewModel);

        await viewModel.RefreshAsync();
        await viewModel.RefreshAsync(isSilent: true);
        await viewModel.RefreshAsync(isSilent: true);

        Assert.Empty(notifications);
        Assert.Empty(viewModel.Notifications);
        Assert.Equal(0, viewModel.UnreadNotificationCount);
    }

    [Fact]
    public async Task RefreshAsync_notifies_once_when_reset_credit_enters_expiry_warning_window()
    {
        var now = DateTimeOffset.Parse("2026-06-28T10:00:00Z");
        var expiresAt = now.AddHours(49);
        var alerts = new StubAlertSettingsService(thresholdPercent: null)
        {
            ResetCreditExpiryLookupEnabled = true,
            ResetCreditExpiryWarningHours = 48
        };
        using var viewModel = new DashboardViewModel(
            new SequenceRateLimitSource(
                CreateSnapshot(now, primaryUsed: 20, weeklyUsed: 30, credits: 2, resetCreditDetails: CreateCreditReport(now, expiresAt)),
                CreateSnapshot(now.AddHours(2), primaryUsed: 20, weeklyUsed: 30, credits: 2, resetCreditDetails: CreateCreditReport(now.AddHours(2), expiresAt)),
                CreateSnapshot(now.AddHours(3), primaryUsed: 20, weeklyUsed: 30, credits: 2, resetCreditDetails: CreateCreditReport(now.AddHours(3), expiresAt))),
            alertSettingsService: alerts);
        var notifications = CaptureNotifications(viewModel);

        await viewModel.RefreshAsync();
        await viewModel.RefreshAsync(isSilent: true);
        await viewModel.RefreshAsync(isSilent: true);

        var notification = Assert.Single(notifications);
        Assert.Equal(TrayNotificationLevel.Warning, notification.Level);
        Assert.Equal("Reset credit expiring soon", notification.Title);
        Assert.Contains("expires in", notification.Text);
        Assert.Single(viewModel.Notifications);
    }

    [Fact]
    public async Task RefreshAsync_reseeds_alert_state_while_notifications_are_disabled()
    {
        var now = DateTimeOffset.Now;
        using var viewModel = new DashboardViewModel(
            new SequenceRateLimitSource(
                CreateSnapshot(now, primaryUsed: 80, weeklyUsed: 30, credits: 2),
                CreateSnapshot(now.AddMinutes(1), primaryUsed: 92, weeklyUsed: 30, credits: 2),
                CreateSnapshot(now.AddMinutes(2), primaryUsed: 94, weeklyUsed: 30, credits: 2),
                CreateSnapshot(now.AddMinutes(3), primaryUsed: 70, weeklyUsed: 30, credits: 2),
                CreateSnapshot(now.AddMinutes(4), primaryUsed: 93, weeklyUsed: 30, credits: 2)),
            alertSettingsService: new StubAlertSettingsService(thresholdPercent: 10, notificationsEnabled: false));
        var notifications = CaptureNotifications(viewModel);

        await viewModel.RefreshAsync();
        await viewModel.RefreshAsync(isSilent: true);
        viewModel.NotificationsEnabled = true;
        await viewModel.RefreshAsync(isSilent: true);
        await viewModel.RefreshAsync(isSilent: true);
        await viewModel.RefreshAsync(isSilent: true);

        var notification = Assert.Single(notifications);
        Assert.Equal("Low Codex capacity", notification.Title);
        Assert.Contains("5-hour 7% left", notification.Text);
        Assert.Single(viewModel.Notifications);
    }

    [Fact]
    public async Task MarkNotificationsReadCommand_clears_unread_badge_without_deleting_history()
    {
        var now = DateTimeOffset.Now;
        using var viewModel = new DashboardViewModel(
            new SequenceRateLimitSource(
                CreateSnapshot(now, primaryUsed: 80, weeklyUsed: 20, credits: 3),
                CreateSnapshot(now.AddMinutes(1), primaryUsed: 92, weeklyUsed: 20, credits: 3)),
            alertSettingsService: new StubAlertSettingsService(thresholdPercent: 10));

        await viewModel.RefreshAsync();
        await viewModel.RefreshAsync(isSilent: true);

        Assert.Equal(1, viewModel.UnreadNotificationCount);

        viewModel.MarkNotificationsReadCommand.Execute(null);

        Assert.Equal(0, viewModel.UnreadNotificationCount);
        Assert.False(viewModel.HasUnreadNotifications);
        Assert.Single(viewModel.Notifications);
    }

    private static List<TrayNotification> CaptureNotifications(DashboardViewModel viewModel)
    {
        var notifications = new List<TrayNotification>();
        viewModel.NotificationRequested += (_, notification) => notifications.Add(notification);
        return notifications;
    }

    private static RateLimitDashboardSnapshot CreateSnapshot(
        DateTimeOffset fetchedAt,
        int primaryUsed,
        int weeklyUsed,
        long? credits,
        DateTimeOffset? primaryReset = null,
        DateTimeOffset? weeklyReset = null,
        ResetCreditReport? resetCreditDetails = null)
    {
        return new RateLimitDashboardSnapshot(
            new[]
            {
                new RateLimitBucket(
                    LimitId: "codex",
                    DisplayName: "Codex",
                    PlanType: "pro",
                    RateLimitReachedType: null,
                    Primary: new RateLimitWindowInfo(RateLimitWindowKind.FiveHour, primaryUsed, 300, primaryReset ?? fetchedAt.AddHours(1)),
                    Secondary: new RateLimitWindowInfo(RateLimitWindowKind.Weekly, weeklyUsed, 10080, weeklyReset ?? fetchedAt.AddDays(3)))
            },
            credits,
            fetchedAt,
            resetCreditDetails);
    }

    private static ResetCreditReport CreateCreditReport(DateTimeOffset fetchedAt, params DateTimeOffset[] expiries) =>
        new(
            expiries.Length,
            expiries
                .Select((expiresAt, index) => new ResetCreditInfo(
                    index == 0 ? "One free rate limit reset" : $"Reset credit {index + 1}",
                    "available",
                    "codex_rate_limits",
                    expiresAt.AddDays(-30),
                    expiresAt))
                .ToArray(),
            fetchedAt);

    private sealed class SequenceRateLimitSource : IRateLimitSource
    {
        private readonly Queue<RateLimitDashboardSnapshot> _snapshots;
        private RateLimitDashboardSnapshot? _last;

        public SequenceRateLimitSource(params RateLimitDashboardSnapshot[] snapshots)
        {
            _snapshots = new Queue<RateLimitDashboardSnapshot>(snapshots);
        }

        public Task<RateLimitDashboardSnapshot> ReadAsync(CancellationToken cancellationToken)
        {
            if (_snapshots.Count > 0)
            {
                _last = _snapshots.Dequeue();
            }

            return Task.FromResult(_last ?? throw new InvalidOperationException("No snapshots configured."));
        }
    }
}
