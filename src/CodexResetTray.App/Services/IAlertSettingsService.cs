namespace CodexResetTray.App.Services;

public interface IAlertSettingsService
{
    int? LowRemainingThresholdPercent { get; set; }
}
