using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace CodexResetTray.App.Services;

/// <summary>
/// Builds the system-tray icon as a true multi-resolution <see cref="Icon"/>
/// (16-64 px frames) so Windows picks a crisp frame at every DPI.
///
/// The mark is a pair of concentric "activity" rings shown at once: the OUTER
/// ring fills with the 5-hour used %, the INNER ring with the weekly used %.
/// Each ring is coloured by the shared usage-state ramp (emerald -> amber ->
/// orange -> red) and backed by a dark halo so both read on light and dark
/// taskbars. A ring with no data shows only its track.
/// </summary>
public static class TrayIconFactory
{
    private static readonly int[] FrameSizes = { 16, 20, 24, 32, 48, 64 };

    private static readonly Color Halo = Color.FromArgb(225, 6, 9, 13);    // dark contrast backing
    private static readonly Color Track = Color.FromArgb(255, 38, 46, 56); // unfilled channel

    public static Icon Create(int? primaryPercent, int? weeklyPercent)
    {
        var bitmaps = new Bitmap[FrameSizes.Length];
        try
        {
            for (var i = 0; i < FrameSizes.Length; i++)
            {
                bitmaps[i] = RenderFrame(FrameSizes[i], primaryPercent, weeklyPercent);
            }

            using var stream = new MemoryStream();
            WriteIcon(stream, bitmaps);
            stream.Position = 0;
            return new Icon(stream);
        }
        finally
        {
            foreach (var bitmap in bitmaps)
            {
                bitmap?.Dispose();
            }
        }
    }

    private static Bitmap RenderFrame(int size, int? primaryPercent, int? weeklyPercent)
    {
        var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(Color.Transparent);
            DrawDualRing(graphics, size, primaryPercent, weeklyPercent);
        }

        return bitmap;
    }

    private static void DrawDualRing(Graphics graphics, int size, int? primaryPercent, int? weeklyPercent)
    {
        var center = new PointF(size / 2f, size / 2f);

        var outerStroke = Math.Max(2.3f, size * 0.13f);
        var innerStroke = Math.Max(2.1f, size * 0.12f);
        var gap = Math.Max(1.1f, size * 0.07f);
        var halo = Math.Max(1.0f, size * 0.05f);
        var inset = halo / 2f + Math.Max(0.7f, size * 0.035f);

        var outerRadius = size / 2f - inset - outerStroke / 2f;
        var innerRadius = outerRadius - outerStroke / 2f - gap - innerStroke / 2f;

        DrawRing(graphics, center, outerRadius, outerStroke, halo, primaryPercent); // 5-hour
        if (innerRadius > innerStroke / 2f)
        {
            DrawRing(graphics, center, innerRadius, innerStroke, halo, weeklyPercent); // weekly
        }
    }

    private static void DrawRing(Graphics graphics, PointF center, float radius, float stroke, float halo, int? percent)
    {
        var rect = new RectangleF(center.X - radius, center.Y - radius, radius * 2f, radius * 2f);

        using (var haloPen = new Pen(Halo, stroke + halo))
        {
            graphics.DrawArc(haloPen, rect, 0f, 360f);
        }

        using (var trackPen = new Pen(Track, stroke))
        {
            graphics.DrawArc(trackPen, rect, 0f, 360f);
        }

        if (percent is { } value)
        {
            var sweep = 360f * (Math.Clamp(value, 0, 100) / 100f);
            if (sweep > 0f)
            {
                using var progressPen = new Pen(RampColor(value), stroke)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };
                graphics.DrawArc(progressPen, rect, -90f, sweep); // fill clockwise from 12 o'clock
            }
        }
    }

    private static Color RampColor(int percent) => percent switch
    {
        >= 100 => Color.FromArgb(255, 244, 107, 107), // #F46B6B  Limited
        >= 90 => Color.FromArgb(255, 251, 140, 59),   // #FB8C3B  Near limit
        >= 70 => Color.FromArgb(255, 251, 191, 36),   // #FBBF24  Watch
        _ => Color.FromArgb(255, 52, 211, 153)        // #34D399  Fresh
    };

    // ---- Minimal in-memory ICO assembly (ICONDIR + ICONDIRENTRY + PNG) -------

    private static void WriteIcon(Stream stream, Bitmap[] frames)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);

        var payloads = new byte[frames.Length][];
        for (var i = 0; i < frames.Length; i++)
        {
            payloads[i] = EncodePng(frames[i]);
        }

        writer.Write((short)0);             // reserved
        writer.Write((short)1);             // type = icon
        writer.Write((short)frames.Length); // image count

        var offset = 6 + (16 * frames.Length);
        for (var i = 0; i < frames.Length; i++)
        {
            var dimension = frames[i].Width;
            writer.Write((byte)(dimension >= 256 ? 0 : dimension));
            writer.Write((byte)(dimension >= 256 ? 0 : dimension));
            writer.Write((byte)0);   // palette
            writer.Write((byte)0);   // reserved
            writer.Write((short)1);  // planes
            writer.Write((short)32); // bpp
            writer.Write(payloads[i].Length);
            writer.Write(offset);
            offset += payloads[i].Length;
        }

        foreach (var payload in payloads)
        {
            writer.Write(payload);
        }

        writer.Flush();
    }

    private static byte[] EncodePng(Bitmap bitmap)
    {
        using var buffer = new MemoryStream();
        bitmap.Save(buffer, System.Drawing.Imaging.ImageFormat.Png);
        return buffer.ToArray();
    }
}
