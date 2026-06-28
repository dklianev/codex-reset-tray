using System.Drawing;
using CodexResetTray.App.Services;

namespace CodexResetTray.Tests;

public sealed class TrayIconFactoryTests
{
    [Fact]
    public void Create_writes_expected_multi_resolution_frames()
    {
        using var icon = TrayIconFactory.Create(primaryPercent: 72, weeklyPercent: 16);
        using var stream = new MemoryStream();

        icon.Save(stream);

        var frames = ReadIconFrames(stream.ToArray());
        Assert.Equal(new[] { 16, 20, 24, 32, 48, 64 }, frames.Select(frame => frame.Width).ToArray());
    }

    [Fact]
    public void Create_keeps_primary_and_weekly_signals_visible_at_16px()
    {
        using var icon = TrayIconFactory.Create(primaryPercent: 92, weeklyPercent: 16);
        using var smallIcon = new Icon(icon, new Size(16, 16));
        using var bitmap = smallIcon.ToBitmap();

        Assert.True(IsColoredSignal(bitmap.GetPixel(7, 7)) || IsColoredSignal(bitmap.GetPixel(8, 8)));
        var centerSignal = CountColoredPixels(bitmap, minRadius: 0, maxRadius: 4);
        var outerSignal = CountColoredPixels(bitmap, minRadius: 5, maxRadius: 8);

        Assert.True(centerSignal >= 6, $"Expected visible weekly center signal, found {centerSignal} pixels.");
        Assert.True(outerSignal >= 12, $"Expected visible 5-hour outer signal, found {outerSignal} pixels.");
    }

    private static List<(int Width, int Height)> ReadIconFrames(byte[] ico)
    {
        using var reader = new BinaryReader(new MemoryStream(ico));
        reader.ReadInt16();
        reader.ReadInt16();
        var count = reader.ReadInt16();
        var frames = new List<(int Width, int Height)>();
        for (var i = 0; i < count; i++)
        {
            var width = reader.ReadByte();
            var height = reader.ReadByte();
            reader.BaseStream.Position += 14;
            frames.Add((width == 0 ? 256 : width, height == 0 ? 256 : height));
        }

        return frames;
    }

    private static int CountColoredPixels(Bitmap bitmap, double minRadius, double maxRadius)
    {
        var count = 0;
        var center = (bitmap.Width - 1) / 2.0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var color = bitmap.GetPixel(x, y);
                var distance = Math.Sqrt(Math.Pow(x - center, 2) + Math.Pow(y - center, 2));
                if (distance >= minRadius
                    && distance <= maxRadius
                    && IsColoredSignal(color))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static bool IsColoredSignal(Color color) =>
        color.A > 150
        && Math.Max(color.R, Math.Max(color.G, color.B)) - Math.Min(color.R, Math.Min(color.G, color.B)) > 45;
}
