using System.Net.Http;
using System.Threading;
using System.Windows.Threading;
using CodexResetTray.App.Services;
using CodexResetTray.App.ViewModels;
using CodexResetTray.Core.Startup;

namespace CodexResetTray.App;

public partial class App : System.Windows.Application
{
    private const string MutexName = "CodexResetTray.SingleInstance";
    private const string ActivationEventName = "CodexResetTray.Activate";

    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _activationEvent;
    private RegisteredWaitHandle? _activationWait;
    private readonly CancellationTokenSource _shutdown = new();
    private DashboardViewModel? _dashboard;
    private MainWindow? _window;
    private TrayController? _tray;
    private DispatcherTimer? _refreshTimer;
    private HttpClient? _expiryHttpClient;
    private bool _ownsSingleInstanceMutex;

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, MutexName, out var isOnlyInstance);
        if (!isOnlyInstance)
        {
            SignalExistingInstance();
            Shutdown();
            return;
        }

        _ownsSingleInstanceMutex = true;

        var startup = new WindowsStartupService();
        var alertSettings = new JsonAlertSettingsService();
        var appServerSource = new CodexAppServerRateLimitSource();
        _expiryHttpClient = new HttpClient();
        var source = new ResetCreditExpiryRateLimitSource(
            appServerSource,
            new WhamResetCreditExpirySource(_expiryHttpClient, new CodexAuthFileCredentialsProvider()),
            alertSettings);
        _dashboard = new DashboardViewModel(source, _shutdown.Token, startup, alertSettings);
        _window = new MainWindow(_dashboard);
        MainWindow = _window;
        _dashboard.ExitRequested += OnExitRequested;
        _dashboard.SnapshotApplied += OnSnapshotApplied;
        RegisterActivationEvent();

        _tray = new TrayController(_window, _dashboard);
        _tray.Initialize();

        _refreshTimer = new DispatcherTimer
        {
            Interval = RefreshCadenceCalculator.DefaultInterval
        };
        _refreshTimer.Tick += OnRefreshTimerTick;
        _dashboard.SetAutoRefreshCadence(_refreshTimer.Interval);

        if (!e.Args.Contains(WindowsStartupCommandFormatter.MinimizedArgument, StringComparer.OrdinalIgnoreCase))
        {
            _window.Show();
        }

        await _dashboard.RefreshAsync(cancellationToken: _shutdown.Token);
        ScheduleNextRefresh();
    }

    private void OnSnapshotApplied(object? sender, EventArgs e)
    {
        if (!_shutdown.IsCancellationRequested)
        {
            ScheduleNextRefresh();
        }
    }

    private async void OnRefreshTimerTick(object? sender, EventArgs e)
    {
        if (_dashboard is null || _refreshTimer is null)
        {
            return;
        }

        _refreshTimer.Stop();
        try
        {
            await _dashboard.RefreshAsync(isSilent: true, cancellationToken: _shutdown.Token);
        }
        finally
        {
            if (!_shutdown.IsCancellationRequested)
            {
                ScheduleNextRefresh();
            }
        }
    }

    private void ScheduleNextRefresh()
    {
        if (_dashboard is null || _refreshTimer is null)
        {
            return;
        }

        var interval = RefreshCadenceCalculator.Calculate(_dashboard.LastSuccessfulSnapshot, DateTimeOffset.Now);
        _refreshTimer.Interval = interval;
        _dashboard.SetAutoRefreshCadence(interval);
        _refreshTimer.Start();
    }

    private void RegisterActivationEvent()
    {
        _activationEvent = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, ActivationEventName);
        _activationWait = ThreadPool.RegisterWaitForSingleObject(
            _activationEvent,
            (_, _) => Dispatcher.BeginInvoke(() => _window?.ShowDashboard()),
            state: null,
            timeout: Timeout.InfiniteTimeSpan,
            executeOnlyOnce: false);
    }

    private static void SignalExistingInstance()
    {
        try
        {
            using var activationEvent = EventWaitHandle.OpenExisting(ActivationEventName);
            activationEvent.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        _window?.ForceClose();
        Shutdown();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _shutdown.Cancel();
        _refreshTimer?.Stop();
        if (_refreshTimer is not null)
        {
            _refreshTimer.Tick -= OnRefreshTimerTick;
        }
        if (_dashboard is not null)
        {
            _dashboard.ExitRequested -= OnExitRequested;
            _dashboard.SnapshotApplied -= OnSnapshotApplied;
        }
        _tray?.Dispose();
        _dashboard?.Dispose();
        _expiryHttpClient?.Dispose();
        _activationWait?.Unregister(null);
        _activationEvent?.Dispose();
        _shutdown.Dispose();
        if (_ownsSingleInstanceMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
        }
        _singleInstanceMutex?.Dispose();

        base.OnExit(e);
    }
}
