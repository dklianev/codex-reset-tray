namespace CodexResetTray.App.Services;

public interface IAlertSettingsService
{
    int? LowRemainingThresholdPercent { get; set; }

    bool NotificationsEnabled { get; set; }

    bool ResetCreditExpiryLookupEnabled { get; set; }

    int ResetCreditExpiryWarningHours { get; set; }
}
