using System.Threading;
using System.Windows.Threading;
using CodexResetTray.App.Services;
using CodexResetTray.App.ViewModels;

namespace CodexResetTray.App;

public partial class App : System.Windows.Application
{
    private const string MutexName = "CodexResetTray.SingleInstance";

    private Mutex? _singleInstanceMutex;
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
            Shutdown();
            return;
        }

        _ownsSingleInstanceMutex = true;

        var source = new CodexAppServerRateLimitSource();
        _dashboard = new DashboardViewModel(source);
        _window = new MainWindow(_dashboard);
        MainWindow = _window;
        _dashboard.ExitRequested += OnExitRequested;

        _tray = new TrayController(_window, _dashboard);
        _tray.Initialize();

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(10)
        };
        _refreshTimer.Tick += async (_, _) => await _dashboard.RefreshAsync(isSilent: true);
        _refreshTimer.Start();

        _window.Show();
        await _dashboard.RefreshAsync();
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        _window?.ForceClose();
        Shutdown();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _refreshTimer?.Stop();
        if (_dashboard is not null)
        {
            _dashboard.ExitRequested -= OnExitRequested;
        }
        _tray?.Dispose();
        _dashboard?.Dispose();
        if (_ownsSingleInstanceMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
        }
        _singleInstanceMutex?.Dispose();

        base.OnExit(e);
    }
}
