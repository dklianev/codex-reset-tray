using System.Diagnostics;
using System.Text.Json;
using CodexResetTray.Core.Protocol;
using CodexResetTray.Core.RateLimits;
using CodexResetTray.Core.Security;

namespace CodexResetTray.App.Services;

public sealed class CodexAppServerRateLimitSource : IRateLimitSource, IDisposable
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(30);
    private readonly object _processLock = new();
    private Process? _activeProcess;
    private bool _isDisposed;

    public async Task<RateLimitDashboardSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ReadTimeout);

        using var process = StartProcess();
        RegisterProcess(process);
        var stderrTask = ReadStderrAsync(process, timeout.Token);

        try
        {
            foreach (var line in AppServerProtocol.CreateStartupMessages(
                         clientName: "codex_limits_tray",
                         title: "Codex Limits Tray",
                         version: "0.1.0"))
            {
                await process.StandardInput.WriteLineAsync(line.AsMemory(), timeout.Token);
            }

            await process.StandardInput.FlushAsync(timeout.Token);

            while (!timeout.IsCancellationRequested)
            {
                var line = await process.StandardOutput.ReadLineAsync(timeout.Token);
                if (line is null)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var snapshot = TryParseRateLimitResponse(line);
                if (snapshot is not null)
                {
                    return snapshot;
                }

                var error = TryParseRateLimitError(line);
                if (error is not null)
                {
                    throw new InvalidOperationException($"Codex app-server rejected the rate-limit read: {error}");
                }
            }

            var stderr = await TryGetStderrAsync(stderrTask);
            throw new InvalidOperationException(BuildNoResponseMessage(process, stderr));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Codex app-server did not return rate limits within 30 seconds.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Codex app-server returned invalid JSONL while reading rate limits.", ex);
        }
        finally
        {
            await StopProcessAsync(process);
            ClearProcess(process);
        }
    }

    private void RegisterProcess(Process process)
    {
        lock (_processLock)
        {
            if (_isDisposed)
            {
                KillProcessTree(process);
                throw new ObjectDisposedException(nameof(CodexAppServerRateLimitSource));
            }

            _activeProcess = process;
        }
    }

    private void ClearProcess(Process process)
    {
        lock (_processLock)
        {
            if (ReferenceEquals(_activeProcess, process))
            {
                _activeProcess = null;
            }
        }
    }

    private static Process StartProcess()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/d /c codex app-server --listen stdio://",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Windows could not start the Codex CLI process.");
            }
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException("Codex CLI was not found on PATH. Install or update Codex, then refresh.", ex);
        }

        return process;
    }

    private static RateLimitDashboardSnapshot? TryParseRateLimitResponse(string line)
    {
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;

        if (!IsResponseFor(root, AppServerProtocol.RateLimitsRequestId)
            || !root.TryGetProperty("result", out var result))
        {
            return null;
        }

        return AppServerRateLimitParser.Parse(result.GetRawText(), DateTimeOffset.Now);
    }

    private static string? TryParseRateLimitError(string line)
    {
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;

        if (!IsResponseFor(root, AppServerProtocol.RateLimitsRequestId)
            || !root.TryGetProperty("error", out var error))
        {
            return null;
        }

        if (error.ValueKind == JsonValueKind.String)
        {
            return SecretRedactor.Redact(error.GetString() ?? "Unknown error");
        }

        if (error.ValueKind == JsonValueKind.Object
            && error.TryGetProperty("message", out var message)
            && message.ValueKind == JsonValueKind.String)
        {
            return SecretRedactor.Redact(message.GetString() ?? "Unknown error");
        }

        return SecretRedactor.Redact(error.GetRawText());
    }

    private static bool IsResponseFor(JsonElement root, int requestId) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty("id", out var id)
        && id.ValueKind == JsonValueKind.Number
        && id.TryGetInt32(out var parsedId)
        && parsedId == requestId;

    private static async Task<string> ReadStderrAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            return SecretRedactor.Redact(await process.StandardError.ReadToEndAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            return string.Empty;
        }
    }

    private static async Task<string> TryGetStderrAsync(Task<string> stderrTask)
    {
        if (!stderrTask.IsCompleted)
        {
            return string.Empty;
        }

        try
        {
            return await stderrTask;
        }
        catch (InvalidOperationException)
        {
            return string.Empty;
        }
    }

    private static string BuildNoResponseMessage(Process process, string stderr)
    {
        var reason = process.HasExited
            ? $"Codex app-server exited with code {process.ExitCode}."
            : "Codex app-server closed the JSONL stream before returning rate limits.";

        return string.IsNullOrWhiteSpace(stderr)
            ? reason
            : $"{reason} {stderr}";
    }

    private static async Task StopProcessAsync(Process process)
    {
        try
        {
            process.StandardInput.Close();
        }
        catch (InvalidOperationException)
        {
        }

        try
        {
            if (!process.HasExited)
            {
                using var shutdownTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await process.WaitForExitAsync(shutdownTimeout.Token);
            }
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                KillProcessTree(process);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    public void Dispose()
    {
        Process? process;

        lock (_processLock)
        {
            _isDisposed = true;
            process = _activeProcess;
            _activeProcess = null;
        }

        if (process is not null)
        {
            KillProcessTree(process);
        }
    }
}
