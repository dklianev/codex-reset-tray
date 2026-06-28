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
        DateTimeOffset? weeklyReset = null)
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
            fetchedAt);
    }

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
