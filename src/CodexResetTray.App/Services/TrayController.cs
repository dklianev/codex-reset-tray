using CodexResetTray.App.ViewModels;
using Forms = System.Windows.Forms;

namespace CodexResetTray.App.Services;

public sealed class TrayController : IDisposable
{
    private readonly MainWindow _window;
    private readonly DashboardViewModel _dashboard;
    private Forms.NotifyIcon? _notifyIcon;

    public TrayController(MainWindow window, DashboardViewModel dashboard)
    {
        _window = window;
        _dashboard = dashboard;
        _dashboard.PropertyChanged += (_, _) => UpdateTooltip();
    }

    public void Initialize()
    {
        var openItem = new Forms.ToolStripMenuItem("Open dashboard", null, (_, _) => _window.ShowDashboard());
        var refreshItem = new Forms.ToolStripMenuItem("Refresh", null, async (_, _) => await _dashboard.RefreshAsync());
        var exitItem = new Forms.ToolStripMenuItem("Exit", null, (_, _) =>
        {
            _window.ForceClose();
            System.Windows.Application.Current.Shutdown();
        });

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "Codex Reset Tray",
            Visible = true,
            ContextMenuStrip = new Forms.ContextMenuStrip()
        };

        _notifyIcon.ContextMenuStrip.Items.Add(openItem);
        _notifyIcon.ContextMenuStrip.Items.Add(refreshItem);
        _notifyIcon.ContextMenuStrip.Items.Add(new Forms.ToolStripSeparator());
        _notifyIcon.ContextMenuStrip.Items.Add(exitItem);
        _notifyIcon.DoubleClick += (_, _) => _window.ShowDashboard();

        UpdateTooltip();
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
    }

    public void Dispose()
    {
        if (_notifyIcon is null)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _notifyIcon = null;
    }
}
