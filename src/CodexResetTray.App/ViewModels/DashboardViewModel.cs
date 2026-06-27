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
    private int _heroPercent;
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

    public int HeroPercent
    {
        get => _heroPercent;
        private set => SetProperty(ref _heroPercent, value);
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
        TrayTooltip = BuildTrayTooltip(snapshot);
    }

    private static string BuildStatusDetail(RateLimitDashboardSnapshot snapshot)
    {
        var main = snapshot.Buckets.FirstOrDefault(bucket => string.Equals(bucket.LimitId, "codex", StringComparison.OrdinalIgnoreCase))
            ?? snapshot.Buckets.FirstOrDefault();

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

    private static string BuildTrayTooltip(RateLimitDashboardSnapshot snapshot)
    {
        var main = snapshot.Buckets.FirstOrDefault(bucket => string.Equals(bucket.LimitId, "codex", StringComparison.OrdinalIgnoreCase))
            ?? snapshot.Buckets.FirstOrDefault();

        if (main is null)
        {
            return "Codex Reset Tray: no rate-limit data";
        }

        var primary = main.Primary?.ResetsAt is { } primaryReset
            ? ResetTimeFormatter.FormatRelative(primaryReset, DateTimeOffset.Now)
            : "unknown";
        var secondary = main.Secondary?.ResetsAt is { } secondaryReset
            ? ResetTimeFormatter.FormatRelative(secondaryReset, DateTimeOffset.Now)
            : "unknown";

        return $"Codex: 5h {primary}, week {secondary}";
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
