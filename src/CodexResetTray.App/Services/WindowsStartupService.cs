using CodexResetTray.Core.Startup;
using Microsoft.Win32;

namespace CodexResetTray.App.Services;

public sealed class WindowsStartupService : IStartupService
{
    private const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CodexResetTray";
    private readonly string _runCommand;

    public WindowsStartupService()
        : this(Environment.ProcessPath ?? throw new InvalidOperationException("The executable path is unavailable."))
    {
    }

    internal WindowsStartupService(string executablePath)
    {
        _runCommand = WindowsStartupCommandFormatter.BuildRunCommand(executablePath);
    }

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunSubKey, writable: false);
            var storedCommand = key?.GetValue(ValueName) as string;
            return WindowsStartupCommandFormatter.MatchesRunCommand(storedCommand, _runCommand);
        }
    }

    public void SetEnabled(bool isEnabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunSubKey, writable: true)
            ?? throw new InvalidOperationException("Could not open the Windows startup registry key.");

        if (isEnabled)
        {
            key.SetValue(ValueName, _runCommand, RegistryValueKind.String);
            return;
        }

        key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
