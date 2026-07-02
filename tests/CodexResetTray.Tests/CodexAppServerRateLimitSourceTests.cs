using CodexResetTray.App.Services;

namespace CodexResetTray.Tests;

public sealed class CodexAppServerRateLimitSourceTests
{
    [Fact]
    public void ResolveCodexCommandPath_ignores_relative_path_entries()
    {
        using var temp = TemporaryDirectory.Create();
        var binPath = Path.Combine(temp.Path, "bin");
        Directory.CreateDirectory(binPath);
        var codexPath = Path.Combine(binPath, "codex.cmd");
        File.WriteAllText(codexPath, "@echo off");

        var pathValue = string.Join(Path.PathSeparator, ".", binPath);

        var resolvedPath = CodexAppServerRateLimitSource.ResolveCodexCommandPath(pathValue, ".CMD;.EXE");

        Assert.Equal(Path.GetFullPath(codexPath), resolvedPath);
    }

    [Fact]
    public void ResolveCodexCommandPath_honors_pathext_order()
    {
        using var temp = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(temp.Path, "codex.exe"), string.Empty);
        var cmdPath = Path.Combine(temp.Path, "codex.cmd");
        File.WriteAllText(cmdPath, "@echo off");

        var resolvedPath = CodexAppServerRateLimitSource.ResolveCodexCommandPath(temp.Path, ".CMD;.EXE");

        Assert.Equal(Path.GetFullPath(cmdPath), resolvedPath);
    }

    [Fact]
    public void ResolveCodexCommandPath_ignores_unsupported_pathext_entries()
    {
        using var temp = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(temp.Path, "codex.ps1"), string.Empty);
        var exePath = Path.Combine(temp.Path, "codex.exe");
        File.WriteAllText(exePath, string.Empty);

        var resolvedPath = CodexAppServerRateLimitSource.ResolveCodexCommandPath(temp.Path, ".PS1;.EXE");

        Assert.Equal(Path.GetFullPath(exePath), resolvedPath);
    }

    [Fact]
    public void ResolveCodexCommandPath_uses_native_fallback_extensions()
    {
        using var temp = TemporaryDirectory.Create();
        var exePath = Path.Combine(temp.Path, "codex.exe");
        File.WriteAllText(exePath, string.Empty);

        var resolvedPath = CodexAppServerRateLimitSource.ResolveCodexCommandPath(temp.Path, string.Empty);

        Assert.Equal(Path.GetFullPath(exePath), resolvedPath);
    }

    [Fact]
    public void CreateStartInfo_runs_exe_directly_with_argument_list()
    {
        var startInfo = CodexAppServerRateLimitSource.CreateStartInfo(@"C:\Tools\codex.exe");

        Assert.Equal(@"C:\Tools\codex.exe", startInfo.FileName);
        Assert.Equal(string.Empty, startInfo.Arguments);
        Assert.Equal(new[] { "app-server", "--listen", "stdio://" }, startInfo.ArgumentList.ToArray());
        AssertCommonStartInfo(startInfo);
    }

    [Fact]
    public void CreateStartInfo_runs_command_shim_through_absolute_cmd()
    {
        var startInfo = CodexAppServerRateLimitSource.CreateStartInfo(@"C:\Tools With Spaces\codex.cmd");

        Assert.Equal("cmd.exe", Path.GetFileName(startInfo.FileName));
        Assert.True(Path.IsPathFullyQualified(startInfo.FileName));
        Assert.Contains("/d /s /c", startInfo.Arguments, StringComparison.Ordinal);
        Assert.Contains(@"""C:\Tools With Spaces\codex.cmd"" ""app-server"" ""--listen"" ""stdio://""", startInfo.Arguments, StringComparison.Ordinal);
        Assert.Empty(startInfo.ArgumentList);
        AssertCommonStartInfo(startInfo);
    }

    private static void AssertCommonStartInfo(System.Diagnostics.ProcessStartInfo startInfo)
    {
        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.RedirectStandardInput);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
        Assert.True(startInfo.CreateNoWindow);
        Assert.False(string.IsNullOrWhiteSpace(startInfo.WorkingDirectory));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"CodexResetTrayTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
