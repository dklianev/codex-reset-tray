using CodexResetTray.Core.Display;
using CodexResetTray.Core.RateLimits;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace CodexResetTray.App.ViewModels;

public sealed class ResetCreditExpiryViewModel
{
    public ResetCreditExpiryViewModel(ResetCreditInfo credit, int ordinal, DateTimeOffset fetchedAt)
    {
        OrdinalText = $"#{ordinal}";
        Title = string.IsNullOrWhiteSpace(credit.Title) ? "Reset credit" : credit.Title;
        ExpiresAt = credit.ExpiresAt;
        ExactText = ResetTimeFormatter.FormatExact(credit.ExpiresAt, TimeZoneInfo.Local);

        var remaining = credit.ExpiresAt - fetchedAt;
        if (remaining <= TimeSpan.Zero)
        {
            RelativeText = "Expired";
            StatusText = "Expired";
            AccentBrush = new MediaSolidColorBrush(MediaColor.FromRgb(244, 107, 107));
            TrayMenuText = $"{OrdinalText} expired | {ExactText}";
        }
        else if (remaining <= TimeSpan.FromHours(48))
        {
            var relative = ResetTimeFormatter.FormatRelative(credit.ExpiresAt, fetchedAt);
            RelativeText = $"Expires in {relative}";
            StatusText = "Soon";
            AccentBrush = new MediaSolidColorBrush(MediaColor.FromRgb(251, 191, 36));
            TrayMenuText = $"{OrdinalText} {relative} | {ExactText}";
        }
        else
        {
            var relative = ResetTimeFormatter.FormatRelative(credit.ExpiresAt, fetchedAt);
            RelativeText = $"Expires in {relative}";
            StatusText = "Available";
            AccentBrush = new MediaSolidColorBrush(MediaColor.FromRgb(52, 211, 153));
            TrayMenuText = $"{OrdinalText} {relative} | {ExactText}";
        }
    }

    public string OrdinalText { get; }

    public string Title { get; }

    public DateTimeOffset ExpiresAt { get; }

    public string RelativeText { get; }

    public string ExactText { get; }

    public string StatusText { get; }

    public string TrayMenuText { get; }

    public MediaBrush AccentBrush { get; }
}
