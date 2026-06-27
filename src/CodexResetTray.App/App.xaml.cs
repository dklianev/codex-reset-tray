using System.Threading;
using System.Windows.Threading;
using CodexResetTray.App.Services;
using CodexResetTray.App.ViewModels;

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

        var source = new CodexAppServerRateLimitSource();
        _dashboard = new DashboardViewModel(source, _shutdown.Token);
        _window = new MainWindow(_dashboard);
        MainWindow = _window;
        _dashboard.ExitRequested += OnExitRequested;
        RegisterActivationEvent();

        _tray = new TrayController(_window, _dashboard);
        _tray.Initialize();

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(10)
        };
        _refreshTimer.Tick += async (_, _) => await _dashboard.RefreshAsync(isSilent: true, cancellationToken: _shutdown.Token);
        _refreshTimer.Start();

        _window.Show();
        await _dashboard.RefreshAsync(cancellationToken: _shutdown.Token);
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
        if (_dashboard is not null)
        {
            _dashboard.ExitRequested -= OnExitRequested;
        }
        _tray?.Dispose();
        _dashboard?.Dispose();
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
