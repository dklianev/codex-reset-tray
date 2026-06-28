namespace CodexResetTray.Core.Display;

public static class RateLimitPercentFormatter
{
    public static int RemainingPercent(int usedPercent) => 100 - Math.Clamp(usedPercent, 0, 100);

    public static string FormatRemainingPercentValue(int usedPercent) => $"{RemainingPercent(usedPercent)}%";

    public static string FormatRemainingPercent(int usedPercent) => $"{RemainingPercent(usedPercent)}% left";

    public static string FormatOptionalRemainingPercentValue(int? usedPercent) =>
        usedPercent is { } value ? FormatRemainingPercentValue(value) : "--";

    public static string FormatOptionalRemainingPercent(int? usedPercent) =>
        usedPercent is { } value ? FormatRemainingPercent(value) : "--";
}
