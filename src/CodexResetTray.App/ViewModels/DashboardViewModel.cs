using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
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
    private readonly AsyncRelayCommand _refreshCommand;
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
    private string _primaryUsageText = "--";
    private string _weeklyUsageText = "--";
    private string _primaryResetText = "Waiting";
    private string _weeklyResetText = "Waiting";
    private string _primaryExactText = string.Empty;
    private string _weeklyExactText = string.Empty;
    private string _signalCaption = "No live snapshot yet";
    private int _heroPercent;
    private int _primaryPercent;
    private int _weeklyPercent;
    private int? _trayPrimaryPercent;
    private int? _trayWeeklyPercent;
    private MediaBrush _statusBrush = new MediaSolidColorBrush(MediaColor.FromRgb(96, 165, 250));

    public DashboardViewModel(IRateLimitSource source)
    {
        _source = source;
        _refreshCommand = new AsyncRelayCommand(() => RefreshAsync(), () => !IsBusy);
        OpenCodexDocsCommand = new AsyncRelayCommand(OpenCodexDocsAsync);
        ExitCommand = new AsyncRelayCommand(() =>
        {
            System.Windows.Application.Current.Shutdown();
            return Task.CompletedTask;
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<BucketViewModel> Buckets { get; } = new();

    public ICommand RefreshCommand => _refreshCommand;

    public ICommand OpenCodexDocsCommand { get; }

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

    public string PrimaryUsageText
    {
        get => _primaryUsageText;
        private set => SetProperty(ref _primaryUsageText, value);
    }

    public string WeeklyUsageText
    {
        get => _weeklyUsageText;
        private set => SetProperty(ref _weeklyUsageText, value);
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

    public int PrimaryPercent
    {
        get => _primaryPercent;
        private set => SetProperty(ref _primaryPercent, value);
    }

    public int WeeklyPercent
    {
        get => _weeklyPercent;
        private set => SetProperty(ref _weeklyPercent, value);
    }

    public MediaBrush StatusBrush
    {
        get => _statusBrush;
        private set => SetProperty(ref _statusBrush, value);
    }

    public async Task RefreshAsync(bool isSilent = false)
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
            var snapshot = await _source.ReadAsync(timeout.Token);
            ApplySnapshot(snapshot);
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException or JsonException or System.ComponentModel.Win32Exception)
        {
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
            >= 100 => new MediaSolidColorBrush(MediaColor.FromRgb(255, 107, 107)),
            >= 90 => new MediaSolidColorBrush(MediaColor.FromRgb(255, 183, 77)),
            >= 70 => new MediaSolidColorBrush(MediaColor.FromRgb(96, 165, 250)),
            _ => new MediaSolidColorBrush(MediaColor.FromRgb(89, 209, 172))
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

        PrimaryPercent = primaryPercent ?? 0;
        WeeklyPercent = weeklyPercent ?? 0;
        TrayPrimaryPercent = primaryPercent;
        TrayWeeklyPercent = weeklyPercent;

        PrimaryUsageText = primaryPercent is { } primary ? $"{primary}%" : "--";
        WeeklyUsageText = weeklyPercent is { } weekly ? $"{weekly}%" : "--";

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

        var primaryPercent = FormatPercent(ClampPercent(main.Primary?.UsedPercent));
        var weeklyPercent = FormatPercent(ClampPercent(main.Secondary?.UsedPercent));
        var primaryReset = FormatResetRelative(main.Primary?.ResetsAt);
        var weeklyReset = FormatResetRelative(main.Secondary?.ResetsAt);

        return $"Codex: 5h {primaryPercent} reset {primaryReset}; wk {weeklyPercent} reset {weeklyReset}";
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
        StatusBrush = new MediaSolidColorBrush(MediaColor.FromRgb(255, 183, 77));
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
        PrimaryPercent = 0;
        WeeklyPercent = 0;
        PrimaryUsageText = "--";
        WeeklyUsageText = "--";
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
        percent is { } value ? $"{value}%" : "--";

    private static string FormatResetRelative(DateTimeOffset? resetAt) =>
        resetAt is { } value ? ResetTimeFormatter.FormatRelative(value, DateTimeOffset.Now) : "unknown";

    private static string FormatResetExact(DateTimeOffset? resetAt) =>
        resetAt is { } value ? ResetTimeFormatter.FormatExact(value, TimeZoneInfo.Local) : string.Empty;

    private static string BuildSignalCaption(int? primaryPercent, int? weeklyPercent, long? credits)
    {
        var loadText = (primaryPercent, weeklyPercent) switch
        {
            ({ } primary, { } weekly) => $"5h {primary}% / weekly {weekly}%",
            ({ } primary, null) => $"5h {primary}% / weekly unknown",
            (null, { } weekly) => $"5h unknown / weekly {weekly}%",
            _ => "usage unknown"
        };
        var creditText = credits is { } value ? $" / credits {value}" : string.Empty;
        return $"{loadText}{creditText}";
    }

    private static string BuildTrayMenuWindowText(string label, RateLimitWindowInfo? window)
    {
        var percent = FormatPercent(ClampPercent(window?.UsedPercent));
        var reset = FormatResetRelative(window?.ResetsAt);
        return $"{label}: {percent} used, resets {reset}";
    }

    private static Task OpenCodexDocsAsync()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://developers.openai.com/codex/app-server.md",
            UseShellExecute = true
        });
        return Task.CompletedTask;
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    public void Dispose()
    {
    }
}
