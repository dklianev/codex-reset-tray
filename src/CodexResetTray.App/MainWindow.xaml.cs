using System.Windows;
using System.Windows.Input;
using CodexResetTray.App.ViewModels;

namespace CodexResetTray.App;

public partial class MainWindow : Window
{
    private bool _forceClose;

    public MainWindow(DashboardViewModel dashboard)
    {
        InitializeComponent();
        DataContext = dashboard;
        TitleBar.MouseLeftButtonDown += OnTitleBarMouseLeftButtonDown;
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

    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // DragMove can race window state changes; ignore.
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

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
