using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace CodexResetTray.App.Converters;

/// <summary>
/// Shared usage-state ramp used by the dashboard gauges and numerics so colour
/// always tracks load: emerald (fresh) -> amber (watch) -> orange (near) ->
/// red (limited). Mirrors the tray icon's ramp.
/// </summary>
internal static class UsageRamp
{
    public static (Color From, Color To, Color Glow) Stops(double percent) => percent switch
    {
        >= 100 => (Rgb(0xFB7185), Rgb(0xEF4444), Rgb(0xF46B6B)),
        >= 90 => (Rgb(0xFDBA74), Rgb(0xF97316), Rgb(0xFB8C3B)),
        >= 70 => (Rgb(0xFCD34D), Rgb(0xF59E0B), Rgb(0xFBBF24)),
        _ => (Rgb(0x6EE7B7), Rgb(0x22D3EE), Rgb(0x34D399)),
    };

    private static Color Rgb(int hex) =>
        Color.FromRgb((byte)((hex >> 16) & 0xFF), (byte)((hex >> 8) & 0xFF), (byte)(hex & 0xFF));
}

/// <summary>percent (int/double) -> diagonal gradient brush for the gauge stroke and big numerics.</summary>
public sealed class PercentToGaugeBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var (from, to, _) = UsageRamp.Stops(ToPercent(value));
        var brush = new LinearGradientBrush(from, to, 45.0);
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    internal static double ToPercent(object? value) => value switch
    {
        int i => i,
        double d => d,
        _ => 0,
    };
}

/// <summary>percent (int/double) -> the state colour, used for the gauge glow.</summary>
public sealed class PercentToGlowColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        UsageRamp.Stops(PercentToGaugeBrushConverter.ToPercent(value)).Glow;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
