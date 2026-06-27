using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace CodexResetTray.App.Services;

public static class TrayIconFactory
{
    private const int IconSize = 64;

    public static Icon Create(int? primaryPercent, int? weeklyPercent)
    {
        using var bitmap = new Bitmap(IconSize, IconSize);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        graphics.Clear(Color.Transparent);

        DrawProgressRing(graphics, primaryPercent);
        DrawCodexGapMark(graphics, primaryPercent);
        DrawWeeklyAccent(graphics, weeklyPercent);

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

    private static void DrawProgressRing(Graphics graphics, int? primaryPercent)
    {
        var ringRect = new RectangleF(9, 9, 46, 46);
        using var underStroke = new Pen(Color.FromArgb(220, 8, 10, 12), 8.2f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        using var track = new Pen(Color.FromArgb(205, 76, 86, 92), 5.8f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        using var progress = new Pen(PickPrimaryColor(primaryPercent), 5.8f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        graphics.DrawArc(underStroke, ringRect, 130, 282);
        graphics.DrawArc(track, ringRect, 130, 282);
        if (primaryPercent is { } primary)
        {
            graphics.DrawArc(progress, ringRect, 130, Math.Min(282, Sweep(primary) * 0.783f));
        }
    }

    private static void DrawCodexGapMark(Graphics graphics, int? primaryPercent)
    {
        var markColor = primaryPercent.HasValue
            ? Color.FromArgb(255, 246, 249, 247)
            : Color.FromArgb(255, 126, 137, 130);

        using var markPen = new Pen(markColor, 4.2f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        graphics.DrawLine(markPen, 31, 21, 31, 43);
        graphics.DrawLine(markPen, 31, 32, 41, 32);
    }

    private static void DrawWeeklyAccent(Graphics graphics, int? weeklyPercent)
    {
        var dotColor = weeklyPercent.HasValue
            ? Color.FromArgb(255, 108, 190, 255)
            : Color.FromArgb(255, 126, 137, 130);

        using var dotBrush = new SolidBrush(dotColor);
        using var dotBorder = new Pen(Color.FromArgb(255, 10, 12, 14), 2);
        var diameter = weeklyPercent switch
        {
            >= 90 => 11,
            >= 70 => 9,
            null => 7,
            _ => 8
        };
        graphics.FillEllipse(dotBrush, 45, 45, diameter, diameter);
        graphics.DrawEllipse(dotBorder, 45, 45, diameter, diameter);
    }

    private static float Sweep(int percent) => Math.Clamp(percent, 0, 100) * 3.6f;

    private static Color PickPrimaryColor(int? primaryPercent) => primaryPercent switch
    {
        >= 100 => Color.FromArgb(255, 255, 107, 107),
        >= 90 => Color.FromArgb(255, 245, 193, 91),
        >= 70 => Color.FromArgb(255, 108, 190, 255),
        _ => Color.FromArgb(255, 105, 225, 178)
    };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
