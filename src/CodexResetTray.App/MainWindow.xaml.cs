using System.Windows;
using CodexResetTray.App.ViewModels;

namespace CodexResetTray.App;

public partial class MainWindow : Window
{
    private bool _forceClose;

    public MainWindow(DashboardViewModel dashboard)
    {
        InitializeComponent();
        DataContext = dashboard;
    }

    public void ShowDashboard()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    public void ForceClose()
    {
        _forceClose = true;
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_forceClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }
}
