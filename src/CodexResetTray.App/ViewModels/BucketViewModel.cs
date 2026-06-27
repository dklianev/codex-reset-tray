using System.Windows.Media;
using CodexResetTray.Core.Display;
using CodexResetTray.Core.RateLimits;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace CodexResetTray.App.ViewModels;

public sealed class BucketViewModel
{
    public BucketViewModel(RateLimitBucket bucket)
    {
        Name = bucket.DisplayName;
        LimitId = bucket.LimitId;
        PlanLabel = string.IsNullOrWhiteSpace(bucket.PlanType) ? "Current plan" : bucket.PlanType;
        StateLabel = bucket.StateLabel;
        StateBrush = bucket.StateLabel switch
        {
            "Limited" => new MediaSolidColorBrush(MediaColor.FromRgb(244, 107, 107)),   // #F46B6B
            "Near limit" => new MediaSolidColorBrush(MediaColor.FromRgb(251, 140, 59)), // #FB8C3B
            "Watch" => new MediaSolidColorBrush(MediaColor.FromRgb(251, 191, 36)),      // #FBBF24
            _ => new MediaSolidColorBrush(MediaColor.FromRgb(54, 211, 153))             // #36D399
        };

        Primary = WindowViewModel.From(bucket.Primary, "5-hour");
        Secondary = WindowViewModel.From(bucket.Secondary, "Weekly");
    }

    public string Name { get; }

    public string LimitId { get; }

    public string PlanLabel { get; }

    public string StateLabel { get; }

    public MediaBrush StateBrush { get; }

    public WindowViewModel Primary { get; }

    public WindowViewModel Secondary { get; }
}

public sealed class WindowViewModel
{
    private WindowViewModel(string label, int usedPercent, string usedText, string resetsText, string exactText)
    {
        Label = label;
        UsedPercent = usedPercent;
        UsedText = usedText;
        ResetsText = resetsText;
        ExactText = exactText;
    }

    public string Label { get; }

    public int UsedPercent { get; }

    public string UsedText { get; }

    public string ResetsText { get; }

    public string ExactText { get; }

    public static WindowViewModel From(RateLimitWindowInfo? window, string fallbackLabel)
    {
        if (window is null)
        {
            return new WindowViewModel(fallbackLabel, 0, "No data", "Waiting for Codex", string.Empty);
        }

        var now = DateTimeOffset.Now;
        var resetText = window.ResetsAt is { } resetsAt
            ? ResetTimeFormatter.FormatRelative(resetsAt, now)
            : "Unknown reset";
        var exactText = window.ResetsAt is { } exact
            ? ResetTimeFormatter.FormatExact(exact, TimeZoneInfo.Local)
            : string.Empty;
        var used = Math.Clamp(window.UsedPercent, 0, 100);

        return new WindowViewModel(window.Label, used, $"{used}% used", resetText, exactText);
    }
}
