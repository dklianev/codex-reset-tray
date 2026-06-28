using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using CodexResetTray.App.Services;
using CodexResetTray.Core.Display;
using CodexResetTray.Core.RateLimits;
using CodexResetTray.Core.Security;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace CodexResetTray.App.ViewModels;

public sealed class DashboardViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IRateLimitSource _source;
    private readonly IStartupService? _startupService;
    private readonly IAlertSettingsService? _alertSettingsService;
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly AsyncRelayCommand _markNotificationsReadCommand;
    private readonly AsyncRelayCommand _toggleNotificationsCommand;
    private readonly CancellationToken _shutdownToken;
    private readonly HashSet<string> _lowAlertActiveKeys = new(StringComparer.OrdinalIgnoreCase);
    private RateLimitDashboardSnapshot? _previousSnapshot;
    private bool _isBusy;
    private string _statusTitle = "Connecting";
    private string _statusDetail = "Reading Codex rate-limit windows";
    private string _resetCreditsText = "Reset credits: checking";
    private string _lastUpdatedText = "Not refreshed yet";
    private string _errorText = string.Empty;
    private string _trayTooltip = "Codex Reset Tray";
    private string _trayStatusText = "5h -- | W --";
    private string _trayMenuFiveHourText = "5-hour: checking";
    private string _trayMenuWeeklyText = "Weekly: checking";
    private string _trayMenuCreditsText = "Reset credits: checking";
    private string _mainLimitName = "Codex";
    private string _primaryRemainingText = "--";
    private string _weeklyRemainingText = "--";
    private string _primaryRemainingValueText = "--";
    private string _weeklyRemainingValueText = "--";
    private string _primaryResetText = "Waiting";
    private string _weeklyResetText = "Waiting";
    private string _primaryExactText = string.Empty;
    private string _weeklyExactText = string.Empty;
    private string _signalCaption = "No live snapshot yet";
    private string _settingsStatusText = "Startup setting unavailable";
    private string _lowRemainingAlertThresholdText = "Low alerts: off";
    private int _heroPercent;
    private int _primaryUsedPercent;
    private int _weeklyUsedPercent;
    private int _primaryRemainingPercent;
    private int _weeklyRemainingPercent;
    private int? _trayPrimaryPercent;
    private int? _trayWeeklyPercent;
    private int? _lowRemainingAlertThresholdPercent;
    private int _unreadNotificationCount;
    private bool _startWithWindowsEnabled;
    private bool _startupSettingAvailable;
    private bool _hasUnreadNotifications;
    private bool _hasNotifications;
    private bool _isNotificationsOpen;
    private MediaBrush _statusBrush = new MediaSolidColorBrush(MediaColor.FromRgb(107, 117, 128));

    public DashboardViewModel(
        IRateLimitSource source,
        CancellationToken shutdownToken = default,
        IStartupService? startupService = null,
        IAlertSettingsService? alertSettingsService = null)
    {
        _source = source;
        _startupService = startupService;
        _alertSettingsService = alertSettingsService;
        _shutdownToken = shutdownToken;
        _refreshCommand = new AsyncRelayCommand(() => RefreshAsync(), () => !IsBusy);
        _markNotificationsReadCommand = new AsyncRelayCommand(MarkNotificationsReadAsync);
        _toggleNotificationsCommand = new AsyncRelayCommand(ToggleNotificationsAsync);
        ExitCommand = new AsyncRelayCommand(() =>
        {
            ExitRequested?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        });
        LoadStartupSetting();
        LoadAlertSettings();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised when the user chooses Exit from the dashboard. The host
    /// (App) force-closes the window and shuts the application down.</summary>
    public event EventHandler? ExitRequested;

    public event EventHandler<TrayNotification>? NotificationRequested;

    public ObservableCollection<BucketViewModel> Buckets { get; } = new();

    public ObservableCollection<NotificationViewModel> Notifications { get; } = new();

    public ICommand RefreshCommand => _refreshCommand;

    public ICommand MarkNotificationsReadCommand => _markNotificationsReadCommand;

    public ICommand ToggleNotificationsCommand => _toggleNotificationsCommand;

    public ICommand ExitCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                _refreshCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusTitle
    {
        get => _statusTitle;
        private set => SetProperty(ref _statusTitle, value);
    }

    public string StatusDetail
    {
        get => _statusDetail;
        private set => SetProperty(ref _statusDetail, value);
    }

    public string ResetCreditsText
    {
        get => _resetCreditsText;
        private set => SetProperty(ref _resetCreditsText, value);
    }

    public string LastUpdatedText
    {
        get => _lastUpdatedText;
        private set => SetProperty(ref _lastUpdatedText, value);
    }

    public string ErrorText
    {
        get => _errorText;
        private set => SetProperty(ref _errorText, value);
    }

    public string TrayTooltip
    {
        get => _trayTooltip;
        private set => SetProperty(ref _trayTooltip, value);
    }

    public string TrayStatusText
    {
        get => _trayStatusText;
        private set => SetProperty(ref _trayStatusText, value);
    }

    public string TrayMenuFiveHourText
    {
        get => _trayMenuFiveHourText;
        private set => SetProperty(ref _trayMenuFiveHourText, value);
    }

    public string TrayMenuWeeklyText
    {
        get => _trayMenuWeeklyText;
        private set => SetProperty(ref _trayMenuWeeklyText, value);
    }

    public string TrayMenuCreditsText
    {
        get => _trayMenuCreditsText;
        private set => SetProperty(ref _trayMenuCreditsText, value);
    }

    public int? TrayPrimaryPercent
    {
        get => _trayPrimaryPercent;
        private set => SetProperty(ref _trayPrimaryPercent, value);
    }

    public int? TrayWeeklyPercent
    {
        get => _trayWeeklyPercent;
        private set => SetProperty(ref _trayWeeklyPercent, value);
    }

    public string MainLimitName
    {
        get => _mainLimitName;
        private set => SetProperty(ref _mainLimitName, value);
    }

    public string PrimaryRemainingText
    {
        get => _primaryRemainingText;
        private set => SetProperty(ref _primaryRemainingText, value);
    }

    public string WeeklyRemainingText
    {
        get => _weeklyRemainingText;
        private set => SetProperty(ref _weeklyRemainingText, value);
    }

    public string PrimaryRemainingValueText
    {
        get => _primaryRemainingValueText;
        private set => SetProperty(ref _primaryRemainingValueText, value);
    }

    public string WeeklyRemainingValueText
    {
        get => _weeklyRemainingValueText;
        private set => SetProperty(ref _weeklyRemainingValueText, value);
    }

    public string PrimaryResetText
    {
        get => _primaryResetText;
        private set => SetProperty(ref _primaryResetText, value);
    }

    public string WeeklyResetText
    {
        get => _weeklyResetText;
        private set => SetProperty(ref _weeklyResetText, value);
    }

    public string PrimaryExactText
    {
        get => _primaryExactText;
        private set => SetProperty(ref _primaryExactText, value);
    }

    public string WeeklyExactText
    {
        get => _weeklyExactText;
        private set => SetProperty(ref _weeklyExactText, value);
    }

    public string SignalCaption
    {
        get => _signalCaption;
        private set => SetProperty(ref _signalCaption, value);
    }

    public int HeroPercent
    {
        get => _heroPercent;
        private set => SetProperty(ref _heroPercent, value);
    }

    public string SettingsStatusText
    {
        get => _settingsStatusText;
        private set => SetProperty(ref _settingsStatusText, value);
    }

    public string LowRemainingAlertThresholdText
    {
        get => _lowRemainingAlertThresholdText;
        private set => SetProperty(ref _lowRemainingAlertThresholdText, value);
    }

    public int? LowRemainingAlertThresholdPercent
    {
        get => _lowRemainingAlertThresholdPercent;
        set => SetLowRemainingAlertThresholdPercent(value);
    }

    public int UnreadNotificationCount
    {
        get => _unreadNotificationCount;
        private set
        {
            if (SetProperty(ref _unreadNotificationCount, value))
            {
                HasUnreadNotifications = value > 0;
                OnPropertyChanged(nameof(UnreadNotificationCountText));
            }
        }
    }

    public string UnreadNotificationCountText => UnreadNotificationCount > 9 ? "9+" : UnreadNotificationCount.ToString();

    public bool HasUnreadNotifications
    {
        get => _hasUnreadNotifications;
        private set => SetProperty(ref _hasUnreadNotifications, value);
    }

    public bool HasNotifications
    {
        get => _hasNotifications;
        private set => SetProperty(ref _hasNotifications, value);
    }

    public bool IsNotificationsOpen
    {
        get => _isNotificationsOpen;
        private set => SetProperty(ref _isNotificationsOpen, value);
    }

    public int PrimaryUsedPercent
    {
        get => _primaryUsedPercent;
        private set => SetProperty(ref _primaryUsedPercent, value);
    }

    public int WeeklyUsedPercent
    {
        get => _weeklyUsedPercent;
        private set => SetProperty(ref _weeklyUsedPercent, value);
    }

    public int PrimaryRemainingPercent
    {
        get => _primaryRemainingPercent;
        private set => SetProperty(ref _primaryRemainingPercent, value);
    }

    public int WeeklyRemainingPercent
    {
        get => _weeklyRemainingPercent;
        private set => SetProperty(ref _weeklyRemainingPercent, value);
    }

    public bool StartupSettingAvailable
    {
        get => _startupSettingAvailable;
        private set => SetProperty(ref _startupSettingAvailable, value);
    }

    public bool StartWithWindowsEnabled
    {
        get => _startWithWindowsEnabled;
        set => SetStartWithWindowsEnabled(value);
    }

    public MediaBrush StatusBrush
    {
        get => _statusBrush;
        private set => SetProperty(ref _statusBrush, value);
    }

    public async Task RefreshAsync(bool isSilent = false, CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        if (!isSilent)
        {
            StatusTitle = "Refreshing";
            StatusDetail = "Asking Codex app-server for read-only limit data";
            ErrorText = string.Empty;
        }

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(35));
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                timeout.Token,
                _shutdownToken,
                cancellationToken);
            var snapshot = await _source.ReadAsync(linkedCancellation.Token);
            ApplySnapshot(snapshot);
            EvaluateNotifications(snapshot);
            _previousSnapshot = snapshot;
        }
        catch (OperationCanceledException) when (_shutdownToken.IsCancellationRequested || cancellationToken.IsCancellationRequested)
        {
        }
        catch (OperationCanceledException)
        {
            ApplyError(new TimeoutException("Codex app-server did not respond in time."));
        }
        catch (Exception) when (_shutdownToken.IsCancellationRequested || cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            // Resident tray app: surface any read failure in the UI instead of
            // letting it escape an async-void caller and tear down the process.
            ApplyError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplySnapshot(RateLimitDashboardSnapshot snapshot)
    {
        Buckets.Clear();
        foreach (var bucket in snapshot.Buckets)
        {
            Buckets.Add(new BucketViewModel(bucket));
        }

        var maxUsed = snapshot.Buckets
            .SelectMany(bucket => new[] { bucket.Primary?.UsedPercent, bucket.Secondary?.UsedPercent })
            .Where(value => value.HasValue)
            .Select(value => Math.Clamp(value!.Value, 0, 100))
            .DefaultIfEmpty(0)
            .Max();

        HeroPercent = maxUsed;
        StatusTitle = maxUsed switch
        {
            >= 100 => "Limited",
            >= 90 => "Near limit",
            >= 70 => "Watching",
            _ => "Ready"
        };
        StatusBrush = maxUsed switch
        {
            >= 100 => new MediaSolidColorBrush(MediaColor.FromRgb(244, 107, 107)), // #F46B6B
            >= 90 => new MediaSolidColorBrush(MediaColor.FromRgb(251, 140, 59)),   // #FB8C3B
            >= 70 => new MediaSolidColorBrush(MediaColor.FromRgb(251, 191, 36)),   // #FBBF24
            _ => new MediaSolidColorBrush(MediaColor.FromRgb(54, 211, 153))        // #36D399
        };
        StatusDetail = BuildStatusDetail(snapshot);
        ResetCreditsText = snapshot.ResetCreditsAvailable is { } credits
            ? $"Reset credits: {credits}"
            : "Reset credits: unavailable";
        LastUpdatedText = $"Updated {ResetTimeFormatter.FormatExact(snapshot.FetchedAt, TimeZoneInfo.Local)}";
        ErrorText = string.Empty;
        ApplyHeroMetrics(snapshot);
    }

    private void ApplyHeroMetrics(RateLimitDashboardSnapshot snapshot)
    {
        var main = SelectMainBucket(snapshot);
        MainLimitName = main?.DisplayName ?? "Codex";

        var primaryPercent = ClampPercent(main?.Primary?.UsedPercent);
        var weeklyPercent = ClampPercent(main?.Secondary?.UsedPercent);
        var primaryRemaining = RateLimitPercentFormatter.RemainingPercent(primaryPercent ?? 100);
        var weeklyRemaining = RateLimitPercentFormatter.RemainingPercent(weeklyPercent ?? 100);

        PrimaryUsedPercent = primaryPercent ?? 0;
        WeeklyUsedPercent = weeklyPercent ?? 0;
        PrimaryRemainingPercent = primaryPercent.HasValue ? primaryRemaining : 0;
        WeeklyRemainingPercent = weeklyPercent.HasValue ? weeklyRemaining : 0;
        TrayPrimaryPercent = primaryPercent;
        TrayWeeklyPercent = weeklyPercent;

        PrimaryRemainingText = RateLimitPercentFormatter.FormatOptionalRemainingPercent(primaryPercent);
        WeeklyRemainingText = RateLimitPercentFormatter.FormatOptionalRemainingPercent(weeklyPercent);
        PrimaryRemainingValueText = RateLimitPercentFormatter.FormatOptionalRemainingPercentValue(primaryPercent);
        WeeklyRemainingValueText = RateLimitPercentFormatter.FormatOptionalRemainingPercentValue(weeklyPercent);

        PrimaryResetText = FormatResetRelative(main?.Primary?.ResetsAt);
        WeeklyResetText = FormatResetRelative(main?.Secondary?.ResetsAt);
        PrimaryExactText = FormatResetExact(main?.Primary?.ResetsAt);
        WeeklyExactText = FormatResetExact(main?.Secondary?.ResetsAt);
        SignalCaption = BuildSignalCaption(primaryPercent, weeklyPercent, snapshot.ResetCreditsAvailable);
        TrayTooltip = BuildTrayTooltip(main, snapshot.ResetCreditsAvailable);
        TrayStatusText = BuildTrayStatusText(main, snapshot.ResetCreditsAvailable);
        TrayMenuFiveHourText = BuildTrayMenuWindowText("5-hour", main?.Primary);
        TrayMenuWeeklyText = BuildTrayMenuWindowText("Weekly", main?.Secondary);
        TrayMenuCreditsText = snapshot.ResetCreditsAvailable is { } credits
            ? $"Reset credits: {credits}"
            : "Reset credits: unavailable";
    }

    private static string BuildStatusDetail(RateLimitDashboardSnapshot snapshot)
    {
        var main = SelectMainBucket(snapshot);

        if (main is null)
        {
            return "No rate-limit buckets were returned.";
        }

        var primary = main.Primary?.ResetsAt is { } primaryReset
            ? $"5-hour resets {ResetTimeFormatter.FormatRelative(primaryReset, DateTimeOffset.Now)}"
            : "5-hour reset unavailable";
        var secondary = main.Secondary?.ResetsAt is { } secondaryReset
            ? $"weekly resets {ResetTimeFormatter.FormatRelative(secondaryReset, DateTimeOffset.Now)}"
            : "weekly reset unavailable";

        return $"{main.DisplayName}: {primary}; {secondary}.";
    }

    private static string BuildTrayTooltip(RateLimitBucket? main, long? credits)
    {
        if (main is null)
        {
            return "Codex Reset Tray: no rate-limit data";
        }

        var primaryPercent = FormatPercentValue(ClampPercent(main.Primary?.UsedPercent));
        var weeklyPercent = FormatPercentValue(ClampPercent(main.Secondary?.UsedPercent));
        var primaryReset = FormatResetRelative(main.Primary?.ResetsAt);
        var weeklyReset = FormatResetRelative(main.Secondary?.ResetsAt);
        var text = $"Codex | 5h {primaryPercent} left >{primaryReset} | W {weeklyPercent} left >{weeklyReset}";
        var withCredits = credits is { } value ? $"{text} | C{value}" : text;

        return withCredits.Length <= 63 ? withCredits : text;
    }

    private static string BuildTrayStatusText(RateLimitBucket? main, long? credits)
    {
        if (main is null)
        {
            return "5h -- | W --";
        }

        var creditText = credits is { } value ? $" | Credits {value}" : string.Empty;
        return $"5h {FormatPercent(ClampPercent(main.Primary?.UsedPercent))} | W {FormatPercent(ClampPercent(main.Secondary?.UsedPercent))}{creditText}";
    }

    private void ApplyError(Exception ex)
    {
        Buckets.Clear();
        HeroPercent = 0;
        StatusTitle = "Needs attention";
        StatusDetail = "Codex rate limits could not be read.";
        StatusBrush = new MediaSolidColorBrush(MediaColor.FromRgb(251, 191, 36)); // #FBBF24
        ErrorText = SecretRedactor.Redact(ex.Message);
        LastUpdatedText = $"Failed {ResetTimeFormatter.FormatExact(DateTimeOffset.Now, TimeZoneInfo.Local)}";
        ResetCreditsText = "Reset credits: unavailable";
        TrayTooltip = "Codex Reset Tray: refresh failed";
        TrayStatusText = "5h -- | W -- | refresh failed";
        TrayMenuFiveHourText = "5-hour: unavailable";
        TrayMenuWeeklyText = "Weekly: unavailable";
        TrayMenuCreditsText = "Reset credits: unavailable";
        TrayPrimaryPercent = null;
        TrayWeeklyPercent = null;
        MainLimitName = "Codex";
        PrimaryUsedPercent = 0;
        WeeklyUsedPercent = 0;
        PrimaryRemainingPercent = 0;
        WeeklyRemainingPercent = 0;
        PrimaryRemainingText = "--";
        WeeklyRemainingText = "--";
        PrimaryRemainingValueText = "--";
        WeeklyRemainingValueText = "--";
        PrimaryResetText = "Unavailable";
        WeeklyResetText = "Unavailable";
        PrimaryExactText = string.Empty;
        WeeklyExactText = string.Empty;
        SignalCaption = "Rate-limit source unavailable";
    }

    private static RateLimitBucket? SelectMainBucket(RateLimitDashboardSnapshot snapshot) =>
        snapshot.Buckets.FirstOrDefault(bucket => string.Equals(bucket.LimitId, "codex", StringComparison.OrdinalIgnoreCase))
        ?? snapshot.Buckets.FirstOrDefault();

    private static int? ClampPercent(int? percent) =>
        percent is { } value ? Math.Clamp(value, 0, 100) : null;

    private static string FormatPercent(int? percent) =>
        RateLimitPercentFormatter.FormatOptionalRemainingPercent(percent);

    private static string FormatPercentValue(int? percent) =>
        RateLimitPercentFormatter.FormatOptionalRemainingPercentValue(percent);

    private static string FormatResetRelative(DateTimeOffset? resetAt) =>
        resetAt is { } value ? ResetTimeFormatter.FormatRelative(value, DateTimeOffset.Now) : "unknown";

    private static string FormatResetExact(DateTimeOffset? resetAt) =>
        resetAt is { } value ? ResetTimeFormatter.FormatExact(value, TimeZoneInfo.Local) : string.Empty;

    private static string BuildSignalCaption(int? primaryPercent, int? weeklyPercent, long? credits)
    {
        var loadText = (primaryPercent, weeklyPercent) switch
        {
            ({ } primary, { } weekly) => $"5h {RateLimitPercentFormatter.FormatRemainingPercent(primary)} / weekly {RateLimitPercentFormatter.FormatRemainingPercent(weekly)}",
            ({ } primary, null) => $"5h {RateLimitPercentFormatter.FormatRemainingPercent(primary)} / weekly unknown",
            (null, { } weekly) => $"5h unknown / weekly {RateLimitPercentFormatter.FormatRemainingPercent(weekly)}",
            _ => "capacity unknown"
        };
        var creditText = credits is { } value ? $" / credits {value}" : string.Empty;
        return $"{loadText}{creditText}";
    }

    private static string BuildTrayMenuWindowText(string label, RateLimitWindowInfo? window)
    {
        var percent = FormatPercent(ClampPercent(window?.UsedPercent));
        if (window?.ResetsAt is not { } resetsAt)
        {
            return $"{label}: {percent} | reset unknown";
        }

        var relative = FormatResetRelative(resetsAt);
        var exact = FormatResetExact(resetsAt);
        var reset = relative == "ready" ? "reset ready" : $"resets in {relative}";
        return $"{label}: {percent} | {reset} ({exact} local)";
    }

    private void LoadAlertSettings()
    {
        try
        {
            _lowRemainingAlertThresholdPercent = _alertSettingsService?.LowRemainingThresholdPercent;
            LowRemainingAlertThresholdText = FormatLowRemainingAlertThresholdText(_lowRemainingAlertThresholdPercent);
        }
        catch (Exception ex)
        {
            _lowRemainingAlertThresholdPercent = null;
            LowRemainingAlertThresholdText = "Low alerts: unavailable";
            ErrorText = SecretRedactor.Redact($"Could not read alert settings: {ex.Message}");
        }
    }

    private void SetLowRemainingAlertThresholdPercent(int? value)
    {
        var normalized = value is { } percent ? Math.Clamp(percent, 1, 99) : (int?)null;
        if (normalized == _lowRemainingAlertThresholdPercent)
        {
            return;
        }

        try
        {
            if (_alertSettingsService is not null)
            {
                _alertSettingsService.LowRemainingThresholdPercent = normalized;
            }

            SetProperty(ref _lowRemainingAlertThresholdPercent, normalized, nameof(LowRemainingAlertThresholdPercent));
            LowRemainingAlertThresholdText = FormatLowRemainingAlertThresholdText(normalized);
            ReseedLowAlertState(_previousSnapshot);
        }
        catch (Exception ex)
        {
            ErrorText = SecretRedactor.Redact($"Could not update alert settings: {ex.Message}");
            OnPropertyChanged(nameof(LowRemainingAlertThresholdPercent));
        }
    }

    private static string FormatLowRemainingAlertThresholdText(int? threshold) =>
        threshold is { } value ? $"Low alerts: {value}% left" : "Low alerts: off";

    private Task MarkNotificationsReadAsync()
    {
        MarkNotificationsRead();
        return Task.CompletedTask;
    }

    private Task ToggleNotificationsAsync()
    {
        IsNotificationsOpen = !IsNotificationsOpen;
        if (IsNotificationsOpen)
        {
            MarkNotificationsRead();
        }

        return Task.CompletedTask;
    }

    private void MarkNotificationsRead()
    {
        foreach (var notification in Notifications)
        {
            notification.MarkRead();
        }

        UnreadNotificationCount = 0;
    }

    private void EvaluateNotifications(RateLimitDashboardSnapshot current)
    {
        var previous = _previousSnapshot;
        if (previous is null)
        {
            ReseedLowAlertState(current);
            return;
        }

        var lowMessages = EvaluateLowRemainingAlerts(previous, current);
        var infoMessages = EvaluateResetAlerts(previous, current);

        TrayNotification? notification = null;
        if (lowMessages.Count > 0)
        {
            notification = new TrayNotification(
                TrayNotificationLevel.Warning,
                "Low Codex capacity",
                string.Join("; ", lowMessages),
                current.FetchedAt);
        }
        else if (infoMessages.Count == 1)
        {
            notification = infoMessages[0];
        }
        else if (infoMessages.Count > 1)
        {
            notification = new TrayNotification(
                TrayNotificationLevel.Info,
                "Codex reset update",
                string.Join("; ", infoMessages.Select(message => message.Text)),
                current.FetchedAt);
        }

        if (notification is not null)
        {
            AddNotification(notification);
        }
    }

    private List<string> EvaluateLowRemainingAlerts(
        RateLimitDashboardSnapshot previous,
        RateLimitDashboardSnapshot current)
    {
        var threshold = LowRemainingAlertThresholdPercent;
        if (threshold is null)
        {
            _lowAlertActiveKeys.Clear();
            return new List<string>();
        }

        var previousWindows = FlattenWindows(previous).ToDictionary(window => window.Key, StringComparer.OrdinalIgnoreCase);
        var messages = new List<string>();
        foreach (var currentWindow in FlattenWindows(current))
        {
            var remaining = RateLimitPercentFormatter.RemainingPercent(currentWindow.Window.UsedPercent);
            if (remaining > threshold.Value)
            {
                _lowAlertActiveKeys.Remove(currentWindow.Key);
                continue;
            }

            var previousRemaining = previousWindows.TryGetValue(currentWindow.Key, out var previousWindow)
                ? RateLimitPercentFormatter.RemainingPercent(previousWindow.Window.UsedPercent)
                : (int?)null;
            var crossesThreshold = previousRemaining is { } value && value > threshold.Value;
            if (crossesThreshold && _lowAlertActiveKeys.Add(currentWindow.Key))
            {
                messages.Add($"{currentWindow.Label} {remaining}% left");
            }
            else
            {
                _lowAlertActiveKeys.Add(currentWindow.Key);
            }
        }

        return messages;
    }

    private List<TrayNotification> EvaluateResetAlerts(
        RateLimitDashboardSnapshot previous,
        RateLimitDashboardSnapshot current)
    {
        var messages = new List<TrayNotification>();
        if (previous.ResetCreditsAvailable is { } oldCredits
            && current.ResetCreditsAvailable is { } newCredits
            && newCredits > oldCredits)
        {
            messages.Add(new TrayNotification(
                TrayNotificationLevel.Info,
                "Manual reset credits added",
                $"+{newCredits - oldCredits} reset credits ({newCredits} total)",
                current.FetchedAt));
        }

        var creditsIncreased = previous.ResetCreditsAvailable is { } previousCredits
            && current.ResetCreditsAvailable is { } currentCredits
            && currentCredits > previousCredits;
        if (creditsIncreased)
        {
            return messages;
        }

        var currentWindows = FlattenWindows(current).ToDictionary(window => window.Key, StringComparer.OrdinalIgnoreCase);
        var resetLabels = new List<string>();
        foreach (var previousWindow in FlattenWindows(previous))
        {
            if (!currentWindows.TryGetValue(previousWindow.Key, out var currentWindow)
                || !IsDirectReset(previousWindow.Window, currentWindow.Window, current.FetchedAt))
            {
                continue;
            }

            var remaining = RateLimitPercentFormatter.FormatRemainingPercent(currentWindow.Window.UsedPercent);
            resetLabels.Add($"{currentWindow.Label} {remaining}");
            _lowAlertActiveKeys.Remove(currentWindow.Key);
        }

        if (resetLabels.Count > 0)
        {
            messages.Add(new TrayNotification(
                TrayNotificationLevel.Info,
                "Codex limits reset",
                string.Join("; ", resetLabels),
                current.FetchedAt));
        }

        return messages;
    }

    private static bool IsDirectReset(
        RateLimitWindowInfo previous,
        RateLimitWindowInfo current,
        DateTimeOffset currentFetchedAt)
    {
        if (previous.ResetsAt is not { } previousReset
            || current.ResetsAt is not { } currentReset
            || currentFetchedAt >= previousReset.AddMinutes(-2)
            || currentReset <= previousReset.AddMinutes(2))
        {
            return false;
        }

        var previousUsed = Math.Clamp(previous.UsedPercent, 0, 100);
        var currentUsed = Math.Clamp(current.UsedPercent, 0, 100);
        return previousUsed - currentUsed >= 50 || (previousUsed >= 90 && currentUsed <= 10);
    }

    private void ReseedLowAlertState(RateLimitDashboardSnapshot? snapshot)
    {
        _lowAlertActiveKeys.Clear();
        if (snapshot is null || LowRemainingAlertThresholdPercent is not { } threshold)
        {
            return;
        }

        foreach (var currentWindow in FlattenWindows(snapshot))
        {
            var remaining = RateLimitPercentFormatter.RemainingPercent(currentWindow.Window.UsedPercent);
            if (remaining <= threshold)
            {
                _lowAlertActiveKeys.Add(currentWindow.Key);
            }
        }
    }

    private void AddNotification(TrayNotification notification)
    {
        Notifications.Insert(0, new NotificationViewModel(notification, isUnread: true));
        while (Notifications.Count > 25)
        {
            Notifications.RemoveAt(Notifications.Count - 1);
        }

        HasNotifications = Notifications.Count > 0;
        UnreadNotificationCount++;
        NotificationRequested?.Invoke(this, notification);
    }

    private static IEnumerable<(string Key, string Label, RateLimitWindowInfo Window)> FlattenWindows(RateLimitDashboardSnapshot snapshot)
    {
        foreach (var bucket in snapshot.Buckets)
        {
            foreach (var window in new[] { bucket.Primary, bucket.Secondary })
            {
                if (window is null)
                {
                    continue;
                }

                var key = $"{bucket.LimitId}:{window.Kind}:{window.WindowDurationMinutes}";
                yield return (key, $"{bucket.DisplayName} {window.Label}", window);
            }
        }
    }

    private void LoadStartupSetting()
    {
        if (_startupService is null)
        {
            StartupSettingAvailable = false;
            SettingsStatusText = "Startup setting unavailable";
            return;
        }

        try
        {
            _startWithWindowsEnabled = _startupService.IsEnabled;
            StartupSettingAvailable = true;
            SettingsStatusText = _startWithWindowsEnabled ? "Starts with Windows" : "Manual start";
        }
        catch (Exception ex)
        {
            StartupSettingAvailable = false;
            SettingsStatusText = "Startup setting unavailable";
            ErrorText = SecretRedactor.Redact($"Could not read startup setting: {ex.Message}");
        }
    }

    private void SetStartWithWindowsEnabled(bool value)
    {
        if (value == _startWithWindowsEnabled)
        {
            return;
        }

        if (_startupService is null || !StartupSettingAvailable)
        {
            OnPropertyChanged(nameof(StartWithWindowsEnabled));
            return;
        }

        try
        {
            _startupService.SetEnabled(value);
            SetProperty(ref _startWithWindowsEnabled, value, nameof(StartWithWindowsEnabled));
            SettingsStatusText = value ? "Starts with Windows" : "Manual start";
        }
        catch (Exception ex)
        {
            SettingsStatusText = "Could not update startup setting";
            ErrorText = SecretRedactor.Redact($"Could not update startup setting: {ex.Message}");
            OnPropertyChanged(nameof(StartWithWindowsEnabled));
        }
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public void Dispose()
    {
        if (_source is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
