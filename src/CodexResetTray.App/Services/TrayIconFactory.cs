using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;

namespace CodexResetTray.App.Services;

public static class TrayIconFactory
{
    public static Icon Create(int? primaryPercent, int? weeklyPercent)
    {
        using var bitmap = new Bitmap(64, 64);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        graphics.Clear(Color.Transparent);

        using var shellBrush = new SolidBrush(Color.FromArgb(255, 11, 13, 16));
        using var strokePen = new Pen(Color.FromArgb(255, 54, 61, 67), 3);
        using var primaryBrush = new SolidBrush(Color.FromArgb(255, 105, 225, 178));
        using var weeklyBrush = new SolidBrush(Color.FromArgb(255, 108, 190, 255));
        using var mutedBrush = new SolidBrush(Color.FromArgb(255, 125, 137, 148));
        using var textBrush = new SolidBrush(Color.FromArgb(255, 246, 248, 250));

        using var outer = RoundedRectangle(new RectangleF(4, 4, 56, 56), 14);
        graphics.FillPath(shellBrush, outer);
        graphics.DrawPath(strokePen, outer);

        DrawUsageRail(graphics, 10, 48, 44, primaryPercent, primaryBrush, mutedBrush);
        DrawUsageRail(graphics, 10, 54, 44, weeklyPercent, weeklyBrush, mutedBrush);

        var label = primaryPercent is { } primary
            ? primary.ToString(CultureInfo.InvariantCulture)
            : "--";
        using var font = new Font("Segoe UI", label.Length > 2 ? 21 : 25, FontStyle.Bold, GraphicsUnit.Pixel);
        var textSize = graphics.MeasureString(label, font);
        graphics.DrawString(label, font, textBrush, 32 - (textSize.Width / 2), 18 - (textSize.Height / 2));

        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            _ = DestroyIcon(handle);
        }
    }

    private static void DrawUsageRail(Graphics graphics, int x, int y, int width, int? percent, Brush activeBrush, Brush mutedBrush)
    {
        using var mutedPath = RoundedRectangle(new RectangleF(x, y, width, 3), 2);
        graphics.FillPath(mutedBrush, mutedPath);

        if (percent is not { } value)
        {
            return;
        }

        var activeWidth = Math.Max(3, width * Math.Clamp(value, 0, 100) / 100);
        using var activePath = RoundedRectangle(new RectangleF(x, y, activeWidth, 3), 2);
        graphics.FillPath(activeBrush, activePath);
    }

    private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;

        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        return path;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
