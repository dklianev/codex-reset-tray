using CodexResetTray.Core.Startup;

namespace CodexResetTray.Tests;

public sealed class WindowsStartupCommandFormatterTests
{
    [Fact]
    public void BuildRunCommand_quotes_executable_path_and_starts_minimized()
    {
        var command = WindowsStartupCommandFormatter.BuildRunCommand(@"C:\Program Files\Codex Reset Tray\CodexResetTray.exe");

        Assert.Equal("\"C:\\Program Files\\Codex Reset Tray\\CodexResetTray.exe\" --minimized", command);
    }

    [Theory]
    [InlineData("\"C:\\Apps\\CodexResetTray.exe\" --minimized", true)]
    [InlineData("C:\\Apps\\CodexResetTray.exe --minimized", false)]
    [InlineData("\"C:\\Apps\\Other.exe\" --minimized", false)]
    [InlineData("\"C:\\Apps\\CodexResetTray.exe\"", false)]
    public void MatchesRunCommand_requires_exact_startup_command(string storedCommand, bool expected)
    {
        var expectedCommand = WindowsStartupCommandFormatter.BuildRunCommand(@"C:\Apps\CodexResetTray.exe");

        Assert.Equal(expected, WindowsStartupCommandFormatter.MatchesRunCommand(storedCommand, expectedCommand));
    }
}
