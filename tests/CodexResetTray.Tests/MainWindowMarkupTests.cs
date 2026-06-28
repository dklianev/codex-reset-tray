namespace CodexResetTray.Tests;

public sealed class MainWindowMarkupTests
{
    [Fact]
    public void Footer_does_not_show_auth_only_expiry_message()
    {
        var xaml = File.ReadAllText(RepoPath("src", "CodexResetTray.App", "MainWindow.xaml"));

        Assert.DoesNotContain("Read-only", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("auth only for opt-in expiry", xaml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Title_bar_drag_uses_native_caption_hit_testing_and_keeps_buttons_clickable()
    {
        var xaml = File.ReadAllText(RepoPath("src", "CodexResetTray.App", "MainWindow.xaml"));
        var codeBehind = File.ReadAllText(RepoPath("src", "CodexResetTray.App", "MainWindow.xaml.cs"));

        Assert.Contains("x:Name=\"NotificationsButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CloseButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("HTCAPTION", codeBehind, StringComparison.Ordinal);
        Assert.Contains("IsChromeDragHit", codeBehind, StringComparison.Ordinal);
        Assert.Contains("NotificationsButton", codeBehind, StringComparison.Ordinal);
        Assert.Contains("CloseButton", codeBehind, StringComparison.Ordinal);
    }

    private static string RepoPath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CodexResetTray.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Could not locate the CodexResetTray repository root.");
        }

        var pathParts = new string[parts.Length + 1];
        pathParts[0] = directory.FullName;
        for (var i = 0; i < parts.Length; i++)
        {
            pathParts[i + 1] = parts[i];
        }

        return Path.Combine(pathParts);
    }
}
