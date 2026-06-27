using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace CodexResetTray.App.Services;

/// <summary>
/// Builds the dynamic system-tray icon as a true multi-resolution
/// <see cref="Icon"/> so Windows can pick a crisp frame at every DPI
/// (the tray renders ~16px @100% up to 32px @200%).
///
/// The icon is a circular usage gauge: a 270° ring whose coloured sweep is
/// proportional to the 5-hour <c>primaryPercent</c> (used %), tinted by the
/// shared usage-state ramp. A small inner dot reports the weekly window in
/// blue. A dark halo behind the ring keeps it legible on both light and dark
/// taskbars.
/// </summary>
public static class TrayIconFactory
{
    /// <summary>Frame sizes baked into the multi-resolution icon, small → large.</summary>
    private static readonly int[] FrameSizes = { 16, 20, 24, 32, 48, 64 };

    // ---- Shared design-brief palette (GDI+ ARGB) -----------------------------
    private static readonly Color Halo = Color.FromArgb(235, 9, 12, 16);       // dark outline / glow
    private static readonly Color Track = Color.FromArgb(255, 42, 50, 60);     // #2A323C stroke, opaque
    private static readonly Color TextSubtle = Color.FromArgb(255, 107, 117, 128); // #6B7580 neutral
    private static readonly Color WeeklyBlue = Color.FromArgb(255, 91, 168, 250);  // #5BA8FA

    // Gauge geometry, expressed as fractions of the frame so every size is sharp.
    private const float StartAngle = 135f;  // open gap at the bottom
    private const float SweepAngle = 270f;

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
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(Color.Transparent);

            DrawGauge(graphics, size, primaryPercent, weeklyPercent);
        }

        return bitmap;
    }

    private static void DrawGauge(Graphics graphics, int size, int? primaryPercent, int? weeklyPercent)
    {
        // Stroke and inset scale with the frame; clamp so 16px stays bold.
        var ringStroke = Math.Max(2.4f, size * 0.165f);
        var haloStroke = ringStroke + Math.Max(1.4f, size * 0.085f);
        var inset = haloStroke / 2f + Math.Max(0.6f, size * 0.045f);
        var ring = RectangleF.FromLTRB(inset, inset, size - inset, size - inset);

        // Dark halo first so the gauge survives on a light taskbar.
        using (var halo = new Pen(Halo, haloStroke)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        })
        {
            graphics.DrawArc(halo, ring, StartAngle, SweepAngle);
        }

        // Neutral track for the unfilled portion of the window.
        using (var track = new Pen(Track, ringStroke)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        })
        {
            graphics.DrawArc(track, ring, StartAngle, SweepAngle);
        }

        // Coloured progress sweep ∝ used %.
        if (primaryPercent is { } primary)
        {
            var fraction = Math.Clamp(primary, 0, 100) / 100f;
            if (fraction > 0f)
            {
                using var progress = new Pen(UsageRampColor(primary), ringStroke)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };
                graphics.DrawArc(progress, ring, StartAngle, SweepAngle * fraction);
            }
        }

        DrawCenter(graphics, size, ring, primaryPercent, weeklyPercent);
    }

    /// <summary>
    /// Center mark: a small calm hub dot so the coloured ring stays the hero and
    /// the silhouette reads unmistakably as a gauge. The hub carries the 5-hour
    /// state colour (neutral when unknown). At ≥24px a short needle ticks toward
    /// the top for extra "gauge" character. A small weekly dot sits in the lower
    /// gap in blue, separated from the hub so the 16px form never clutters.
    /// </summary>
    private static void DrawCenter(Graphics graphics, int size, RectangleF ring, int? primaryPercent, int? weeklyPercent)
    {
        var center = new PointF(size / 2f, size / 2f);
        var hubColor = primaryPercent is { } primary ? UsageRampColor(primary) : TextSubtle;

        // A short needle from the hub toward the top, only where it stays crisp.
        if (size >= 24 && primaryPercent is { } p)
        {
            var needleLen = Math.Max(3f, size * 0.2f);
            using (var needleHalo = new Pen(Halo, Math.Max(2.4f, size * 0.11f))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            })
            {
                graphics.DrawLine(needleHalo, center.X, center.Y, center.X, center.Y - needleLen);
            }

            using var needle = new Pen(UsageRampColor(p), Math.Max(1.4f, size * 0.055f))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            graphics.DrawLine(needle, center.X, center.Y, center.X, center.Y - needleLen);
        }

        // Small haloed hub dot.
        var hubRadius = Math.Max(1.3f, size * 0.085f);
        using (var hubHalo = new SolidBrush(Halo))
        {
            var haloR = hubRadius + Math.Max(0.7f, size * 0.04f);
            graphics.FillEllipse(hubHalo, center.X - haloR, center.Y - haloR, haloR * 2f, haloR * 2f);
        }

        using (var hub = new SolidBrush(hubColor))
        {
            graphics.FillEllipse(hub, center.X - hubRadius, center.Y - hubRadius, hubRadius * 2f, hubRadius * 2f);
        }

        // Weekly indicator: a small blue dot centred in the bottom gap.
        var weeklyColor = weeklyPercent.HasValue ? WeeklyBlue : TextSubtle;
        var weeklyR = Math.Max(1.2f, size * 0.085f);
        var weeklyCenter = new PointF(size / 2f, ring.Bottom - Math.Max(0.5f, size * 0.02f));
        using (var weeklyHalo = new SolidBrush(Halo))
        {
            var haloR = weeklyR + Math.Max(0.6f, size * 0.035f);
            graphics.FillEllipse(weeklyHalo, weeklyCenter.X - haloR, weeklyCenter.Y - haloR, haloR * 2f, haloR * 2f);
        }

        using var weekly = new SolidBrush(weeklyColor);
        graphics.FillEllipse(weekly, weeklyCenter.X - weeklyR, weeklyCenter.Y - weeklyR, weeklyR * 2f, weeklyR * 2f);
    }

    /// <summary>Shared usage-state ramp driving the ring colour from used %.</summary>
    private static Color UsageRampColor(int usedPercent) => usedPercent switch
    {
        >= 100 => Color.FromArgb(255, 244, 107, 107), // #F46B6B  Limited
        >= 90 => Color.FromArgb(255, 251, 140, 59),   // #FB8C3B  Near limit
        >= 70 => Color.FromArgb(255, 251, 191, 36),   // #FBBF24  Watch
        _ => Color.FromArgb(255, 54, 211, 153)        // #36D399  Fresh / Ready
    };

    // ---- Minimal in-memory ICO assembly (ICONDIR + ICONDIRENTRY + PNG/BMP) ---

    private static void WriteIcon(Stream stream, Bitmap[] frames)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);

        var payloads = new byte[frames.Length][];
        for (var i = 0; i < frames.Length; i++)
        {
            payloads[i] = EncodePng(frames[i]);
        }

        // ICONDIR
        writer.Write((short)0);             // reserved
        writer.Write((short)1);             // type = icon
        writer.Write((short)frames.Length); // image count

        // ICONDIRENTRY records follow the directory header; image data is appended after.
        var offset = 6 + (16 * frames.Length);
        for (var i = 0; i < frames.Length; i++)
        {
            var dimension = frames[i].Width;
            writer.Write((byte)(dimension >= 256 ? 0 : dimension)); // width  (0 == 256)
            writer.Write((byte)(dimension >= 256 ? 0 : dimension)); // height (0 == 256)
            writer.Write((byte)0);                                  // palette count
            writer.Write((byte)0);                                  // reserved
            writer.Write((short)1);                                 // colour planes
            writer.Write((short)32);                                // bits per pixel
            writer.Write(payloads[i].Length);                       // bytes of image data
            writer.Write(offset);                                   // offset to image data
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
