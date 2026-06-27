using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace CodexResetTray.App.Services;

/// <summary>
/// Builds the system-tray icon as a true multi-resolution <see cref="Icon"/>
/// (16-64 px frames) so Windows picks a crisp frame at every DPI.
///
/// The mark is deliberately a single, bold signal: a circular ring that fills
/// clockwise with the 5-hour used %, coloured by the shared usage-state ramp
/// (emerald -> amber -> orange -> red). No inner glyphs compete with it, so the
/// state reads instantly at 16 px on both light and dark taskbars.
/// </summary>
public static class TrayIconFactory
{
    private static readonly int[] FrameSizes = { 16, 20, 24, 32, 48, 64 };

    private static readonly Color Halo = Color.FromArgb(225, 6, 9, 13);     // dark contrast ring
    private static readonly Color Track = Color.FromArgb(255, 36, 44, 55);  // unfilled channel
    private static readonly Color Unknown = Color.FromArgb(255, 96, 106, 120);

    public static Icon Create(int? primaryPercent)
    {
        var bitmaps = new Bitmap[FrameSizes.Length];
        try
        {
            for (var i = 0; i < FrameSizes.Length; i++)
            {
                bitmaps[i] = RenderFrame(FrameSizes[i], primaryPercent);
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

    private static Bitmap RenderFrame(int size, int? primaryPercent)
    {
        var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(Color.Transparent);
            DrawRing(graphics, size, primaryPercent);
        }

        return bitmap;
    }

    private static void DrawRing(Graphics graphics, int size, int? primaryPercent)
    {
        // Bold stroke that survives 16 px; inset keeps the ring fully inside the frame.
        var stroke = Math.Max(3f, size * 0.2f);
        var haloStroke = stroke + Math.Max(1.3f, size * 0.06f);
        var inset = haloStroke / 2f + Math.Max(0.7f, size * 0.04f);
        var rect = RectangleF.FromLTRB(inset, inset, size - inset, size - inset);

        const float startAngle = -90f; // 12 o'clock
        const float fullSweep = 360f;

        using (var haloPen = new Pen(Halo, haloStroke))
        {
            graphics.DrawArc(haloPen, rect, 0, fullSweep);
        }

        using (var trackPen = new Pen(Track, stroke))
        {
            graphics.DrawArc(trackPen, rect, 0, fullSweep);
        }

        if (primaryPercent is { } percent)
        {
            var sweep = fullSweep * (Math.Clamp(percent, 0, 100) / 100f);
            if (sweep > 0f)
            {
                using var progressPen = new Pen(RampColor(percent), stroke)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };
                graphics.DrawArc(progressPen, rect, startAngle, sweep);
            }
        }
        else
        {
            // No data: a small neutral tick at the top so the icon never looks blank.
            using var tickPen = new Pen(Unknown, stroke)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            graphics.DrawArc(tickPen, rect, startAngle, 14f);
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
