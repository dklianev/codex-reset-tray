using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using CodexResetTray.App.ViewModels;
using Point = System.Windows.Point;

namespace CodexResetTray.App;

public partial class MainWindow : Window
{
    private const double ShadowMargin = 18.0; // transparent margin around the visible shell
    private const double ResizeBand = 8.0;    // grab thickness just inside the visible edge
    private const double CornerRadius = 18.0; // matches the shell border radius

    private const int WM_NCHITTEST = 0x0084;
    private const int HTCAPTION = 2;
    private const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13,
        HTTOPRIGHT = 14, HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;

    private bool _forceClose;

    public MainWindow(DashboardViewModel dashboard)
    {
        InitializeComponent();
        DataContext = dashboard;
        TitleBar.MouseLeftButtonDown += OnTitleBarMouseLeftButtonDown;
        MeshLayer.SizeChanged += OnMeshLayerSizeChanged;
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

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WndProc);
        }
    }

    private static void OnMeshLayerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var layer = (FrameworkElement)sender;
        layer.Clip = new RectangleGeometry(new Rect(e.NewSize), CornerRadius, CornerRadius);
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

    // Frameless window: synthesize native resize on the visible edges/corners.
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_NCHITTEST)
        {
            return IntPtr.Zero;
        }

        var code = ResizeHitTest(lParam);
        if (code != 0)
        {
            handled = true;
            return new IntPtr(code);
        }

        if (IsChromeDragHit(lParam))
        {
            handled = true;
            return new IntPtr(HTCAPTION);
        }

        return IntPtr.Zero; // fall through to WPF's default client/transparent hit-testing
    }

    private int ResizeHitTest(IntPtr lParam)
    {
        if (!TryGetWindowPoint(lParam, out var p))
        {
            return 0;
        }

        var w = ActualWidth;
        var h = ActualHeight;
        var left = p.X >= ShadowMargin && p.X < ShadowMargin + ResizeBand;
        var right = p.X > w - ShadowMargin - ResizeBand && p.X <= w - ShadowMargin;
        var top = p.Y >= ShadowMargin && p.Y < ShadowMargin + ResizeBand;
        var bottom = p.Y > h - ShadowMargin - ResizeBand && p.Y <= h - ShadowMargin;

        return (top, bottom, left, right) switch
        {
            (true, _, true, _) => HTTOPLEFT,
            (true, _, _, true) => HTTOPRIGHT,
            (_, true, true, _) => HTBOTTOMLEFT,
            (_, true, _, true) => HTBOTTOMRIGHT,
            (true, _, _, _) => HTTOP,
            (_, true, _, _) => HTBOTTOM,
            (_, _, true, _) => HTLEFT,
            (_, _, _, true) => HTRIGHT,
            _ => 0,
        };
    }

    private bool IsChromeDragHit(IntPtr lParam)
    {
        if (!TryGetWindowPoint(lParam, out var p))
        {
            return false;
        }

        if (IsPointInside(NotificationsButton, p)
            || IsPointInside(CloseButton, p)
            || IsPointInside(NotificationPanel, p))
        {
            return false;
        }

        var titleBottom = ShadowMargin + 52.0;
        try
        {
            var titleOrigin = TitleBar.TransformToAncestor(this).Transform(new Point(0, 0));
            titleBottom = Math.Max(titleBottom, titleOrigin.Y + TitleBar.ActualHeight);
        }
        catch (InvalidOperationException)
        {
            // Keep the conservative fallback for early native hit-tests.
        }

        return p.X >= ShadowMargin
            && p.X <= ActualWidth - ShadowMargin
            && p.Y >= ShadowMargin + ResizeBand
            && p.Y <= titleBottom;
    }

    private bool TryGetWindowPoint(IntPtr lParam, out Point point)
    {
        var packed = lParam.ToInt64();
        var screen = new Point((short)(packed & 0xFFFF), (short)((packed >> 16) & 0xFFFF));

        try
        {
            point = PointFromScreen(screen);
            return true;
        }
        catch (InvalidOperationException)
        {
            point = default;
            return false;
        }
    }

    private bool IsPointInside(FrameworkElement element, Point windowPoint)
    {
        if (!element.IsVisible || element.ActualWidth <= 0 || element.ActualHeight <= 0)
        {
            return false;
        }

        try
        {
            var origin = element.TransformToAncestor(this).Transform(new Point(0, 0));
            var bounds = new Rect(origin, new System.Windows.Size(element.ActualWidth, element.ActualHeight));
            return bounds.Contains(windowPoint);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
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
