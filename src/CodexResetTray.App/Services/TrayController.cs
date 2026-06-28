using System.ComponentModel;
using CodexResetTray.App.ViewModels;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace CodexResetTray.App.Services;

public sealed class TrayController : IDisposable
{
    private readonly MainWindow _window;
    private readonly DashboardViewModel _dashboard;
    private Forms.NotifyIcon? _notifyIcon;
    private Forms.ToolStripMenuItem? _startWithWindowsItem;
    private Forms.ToolStripMenuItem? _fiveHourItem;
    private Forms.ToolStripMenuItem? _weeklyItem;
    private Forms.ToolStripMenuItem? _creditsItem;
    private Forms.ToolStripMenuItem? _notificationsItem;
    private Forms.ToolStripMenuItem? _alertsItem;
    private readonly List<(int? Threshold, Forms.ToolStripMenuItem Item)> _alertThresholdItems = new();
    private System.Drawing.Icon? _currentIcon;
    private bool _updateQueued;

    public TrayController(MainWindow window, DashboardViewModel dashboard)
    {
        _window = window;
        _dashboard = dashboard;
        _dashboard.PropertyChanged += OnDashboardPropertyChanged;
        _dashboard.NotificationRequested += OnNotificationRequested;
    }

    public void Initialize()
    {
        var openItem = new Forms.ToolStripMenuItem("Open dashboard", null, (_, _) => _window.ShowDashboard());
        var refreshItem = new Forms.ToolStripMenuItem("Refresh", null, async (_, _) => await _dashboard.RefreshAsync());
        _startWithWindowsItem = new Forms.ToolStripMenuItem("Start with Windows", null, (_, _) =>
        {
            _dashboard.StartWithWindowsEnabled = !_dashboard.StartWithWindowsEnabled;
            UpdateTooltip();
        });
        _notificationsItem = new Forms.ToolStripMenuItem(_dashboard.NotificationsEnabledText, null, (_, _) =>
        {
            _dashboard.NotificationsEnabled = !_dashboard.NotificationsEnabled;
            UpdateTooltip();
        });
        _alertsItem = BuildAlertsMenu();
        var exitItem = new Forms.ToolStripMenuItem("Exit", null, (_, _) =>
        {
            _window.ForceClose();
            System.Windows.Application.Current.Shutdown();
        });
        _fiveHourItem = new Forms.ToolStripMenuItem(_dashboard.TrayMenuFiveHourText)
        {
            Enabled = false
        };
        _weeklyItem = new Forms.ToolStripMenuItem(_dashboard.TrayMenuWeeklyText)
        {
            Enabled = false
        };
        _creditsItem = new Forms.ToolStripMenuItem(_dashboard.TrayMenuCreditsText)
        {
            Enabled = false
        };
        _currentIcon = TrayIconFactory.Create(_dashboard.TrayPrimaryPercent, _dashboard.TrayWeeklyPercent);

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _currentIcon,
            Text = "Codex Reset Tray",
            Visible = true,
            ContextMenuStrip = new Forms.ContextMenuStrip()
        };

        _notifyIcon.ContextMenuStrip.Items.Add(_fiveHourItem);
        _notifyIcon.ContextMenuStrip.Items.Add(_weeklyItem);
        _notifyIcon.ContextMenuStrip.Items.Add(_creditsItem);
        _notifyIcon.ContextMenuStrip.Items.Add(new Forms.ToolStripSeparator());
        _notifyIcon.ContextMenuStrip.Items.Add(openItem);
        _notifyIcon.ContextMenuStrip.Items.Add(refreshItem);
        _notifyIcon.ContextMenuStrip.Items.Add(_startWithWindowsItem);
        _notifyIcon.ContextMenuStrip.Items.Add(_notificationsItem);
        _notifyIcon.ContextMenuStrip.Items.Add(_alertsItem);
        _notifyIcon.ContextMenuStrip.Items.Add(new Forms.ToolStripSeparator());
        _notifyIcon.ContextMenuStrip.Items.Add(exitItem);
        _notifyIcon.DoubleClick += (_, _) => _window.ShowDashboard();

        UpdateTooltip();
    }

    private void OnDashboardPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(DashboardViewModel.TrayTooltip)
            or nameof(DashboardViewModel.TrayStatusText)
            or nameof(DashboardViewModel.TrayMenuFiveHourText)
            or nameof(DashboardViewModel.TrayMenuWeeklyText)
            or nameof(DashboardViewModel.TrayMenuCreditsText)
            or nameof(DashboardViewModel.TrayPrimaryPercent)
            or nameof(DashboardViewModel.TrayWeeklyPercent)
            or nameof(DashboardViewModel.StartWithWindowsEnabled)
            or nameof(DashboardViewModel.StartupSettingAvailable)
            or nameof(DashboardViewModel.NotificationsEnabled)
            or nameof(DashboardViewModel.NotificationsEnabledText)
            or nameof(DashboardViewModel.LowRemainingAlertThresholdPercent)
            or nameof(DashboardViewModel.LowRemainingAlertThresholdText)))
        {
            return;
        }

        QueueUpdate();
    }

    private void QueueUpdate()
    {
        if (_updateQueued)
        {
            return;
        }

        _updateQueued = true;
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _updateQueued = false;
            UpdateTooltip();
        }, DispatcherPriority.Background);
    }

    private void UpdateTooltip()
    {
        if (_notifyIcon is null)
        {
            return;
        }

        var text = string.IsNullOrWhiteSpace(_dashboard.TrayTooltip)
            ? "Codex Reset Tray"
            : _dashboard.TrayTooltip;

        _notifyIcon.Text = text.Length > 63 ? text[..63] : text;
        if (_fiveHourItem is not null)
        {
            _fiveHourItem.Text = _dashboard.TrayMenuFiveHourText;
        }

        if (_weeklyItem is not null)
        {
            _weeklyItem.Text = _dashboard.TrayMenuWeeklyText;
        }

        if (_creditsItem is not null)
        {
            _creditsItem.Text = _dashboard.TrayMenuCreditsText;
        }

        if (_startWithWindowsItem is not null)
        {
            _startWithWindowsItem.Checked = _dashboard.StartWithWindowsEnabled;
            _startWithWindowsItem.Enabled = _dashboard.StartupSettingAvailable;
        }

        if (_notificationsItem is not null)
        {
            _notificationsItem.Text = _dashboard.NotificationsEnabledText;
            _notificationsItem.Checked = _dashboard.NotificationsEnabled;
        }

        if (_alertsItem is not null)
        {
            _alertsItem.Text = _dashboard.LowRemainingAlertThresholdText;
            _alertsItem.Enabled = _dashboard.NotificationsEnabled;
            foreach (var (threshold, item) in _alertThresholdItems)
            {
                item.Checked = threshold == _dashboard.LowRemainingAlertThresholdPercent;
            }
        }

        var previousIcon = _currentIcon;
        _currentIcon = TrayIconFactory.Create(_dashboard.TrayPrimaryPercent, _dashboard.TrayWeeklyPercent);
        _notifyIcon.Icon = _currentIcon;
        previousIcon?.Dispose();
    }

    private Forms.ToolStripMenuItem BuildAlertsMenu()
    {
        _alertThresholdItems.Clear();
        var alertsItem = new Forms.ToolStripMenuItem(_dashboard.LowRemainingAlertThresholdText);
        AddThresholdItem(alertsItem, null, "Off");
        alertsItem.DropDownItems.Add(new Forms.ToolStripSeparator());
        foreach (var threshold in new[] { 5, 10, 15, 20, 25 })
        {
            AddThresholdItem(alertsItem, threshold, $"{threshold}% left");
        }

        return alertsItem;
    }

    private void AddThresholdItem(Forms.ToolStripMenuItem parent, int? threshold, string label)
    {
        var item = new Forms.ToolStripMenuItem(label, null, (_, _) =>
        {
            _dashboard.LowRemainingAlertThresholdPercent = threshold;
            UpdateTooltip();
        });
        _alertThresholdItems.Add((threshold, item));
        parent.DropDownItems.Add(item);
    }

    private void OnNotificationRequested(object? sender, TrayNotification notification)
    {
        if (_notifyIcon is null || !_dashboard.NotificationsEnabled)
        {
            return;
        }

        var icon = notification.Level == TrayNotificationLevel.Warning
            ? Forms.ToolTipIcon.Warning
            : Forms.ToolTipIcon.Info;
        _notifyIcon.ShowBalloonTip(8000, notification.Title, notification.Text, icon);
    }

    public void Dispose()
    {
        if (_notifyIcon is null)
        {
            return;
        }

        _dashboard.PropertyChanged -= OnDashboardPropertyChanged;
        _dashboard.NotificationRequested -= OnNotificationRequested;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _notifyIcon = null;
        _currentIcon?.Dispose();
        _currentIcon = null;
    }
}
