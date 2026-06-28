using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using CodexResetTray.Core.Display;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace CodexResetTray.App.ViewModels;

public enum TrayNotificationLevel
{
    Info,
    Warning
}

public sealed record TrayNotification(
    TrayNotificationLevel Level,
    string Title,
    string Text,
    DateTimeOffset CreatedAt);

public sealed class NotificationViewModel : INotifyPropertyChanged
{
    private bool _isUnread;

    public NotificationViewModel(TrayNotification notification, bool isUnread)
    {
        Level = notification.Level;
        Title = notification.Title;
        Text = notification.Text;
        CreatedAt = notification.CreatedAt;
        _isUnread = isUnread;
        AccentBrush = notification.Level switch
        {
            TrayNotificationLevel.Warning => new MediaSolidColorBrush(MediaColor.FromRgb(251, 191, 36)),
            _ => new MediaSolidColorBrush(MediaColor.FromRgb(52, 211, 153))
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public TrayNotificationLevel Level { get; }

    public string Title { get; }

    public string Text { get; }

    public DateTimeOffset CreatedAt { get; }

    public string TimestampText => ResetTimeFormatter.FormatExact(CreatedAt, TimeZoneInfo.Local);

    public MediaBrush AccentBrush { get; }

    public bool IsUnread
    {
        get => _isUnread;
        private set
        {
            if (_isUnread == value)
            {
                return;
            }

            _isUnread = value;
            OnPropertyChanged();
        }
    }

    public void MarkRead() => IsUnread = false;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
