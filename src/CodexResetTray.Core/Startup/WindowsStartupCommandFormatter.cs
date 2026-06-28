namespace CodexResetTray.Core.Startup;

public static class WindowsStartupCommandFormatter
{
    public const string MinimizedArgument = "--minimized";

    public static string BuildRunCommand(string executablePath) => $"\"{executablePath}\" {MinimizedArgument}";

    public static bool MatchesRunCommand(string? storedCommand, string expectedCommand) =>
        string.Equals(storedCommand, expectedCommand, StringComparison.Ordinal);
}
