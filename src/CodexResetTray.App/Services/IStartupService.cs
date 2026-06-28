namespace CodexResetTray.App.Services;

public interface IStartupService
{
    bool IsEnabled { get; }

    void SetEnabled(bool isEnabled);
}
