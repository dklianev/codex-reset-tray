# Codex Reset Tray

Codex Reset Tray is a lightweight Windows tray app that shows your Codex rate-limit reset windows without reading `.codex/auth.json`.

It uses the official Codex app-server protocol, asks only for `account/rateLimits/read`, and displays the two windows people care about most: the 5-hour reset and the weekly reset.

## Features

- Tray-first Windows app with a compact WPF dashboard.
- Shows 5-hour and weekly reset timers, exact local reset time, used percent, and reset credit count.
- Shows live 5-hour and weekly percentages in the tray tooltip and tray context menu.
- Draws a crisp, multi-resolution dynamic tray icon: a usage gauge ring colored by your 5-hour load, with a weekly indicator, legible on light and dark taskbars.
- Ships a branded multi-resolution application/window icon, generated reproducibly from `packaging/generate-app-icon.ps1`.
- Uses `codex app-server --listen stdio://` instead of scraping logs or reading auth files.
- Read-only RPC allowlist: `initialize`, `initialized`, and `account/rateLimits/read`.
- Degraded states for missing CLI, unsupported app-server, timeouts, malformed JSON, and unavailable buckets.
- No telemetry, no cloud backend, no account tokens stored by this app.
- Core parser covered by xUnit tests.

## Requirements

- Windows 10 or Windows 11.
- Codex CLI installed and available on `PATH`.
- .NET 10 SDK for development builds.

## Quick Start

```powershell
dotnet restore .\CodexResetTray.slnx
dotnet test .\CodexResetTray.slnx
dotnet run --project .\src\CodexResetTray.App\CodexResetTray.App.csproj
```

The app opens a small dashboard and adds a tray icon. Closing the dashboard hides it; use the tray menu to open, refresh, or exit.

## Build A Portable Release

```powershell
.\packaging\publish.ps1
```

The script publishes a self-contained `win-x64` build and creates a zip under `artifacts\release`.

## Architecture

```text
src/
  CodexResetTray.Core/
    Protocol/        JSONL request creation and rate-limit parsing
    RateLimits/      version-tolerant domain models
    Display/         reset time formatting
    Security/        defensive redaction helpers
  CodexResetTray.App/
    Services/        Codex app-server reader and tray integration
    ViewModels/      UI state mapping and commands
    Resources/       WPF theme tokens and control styles
tests/
  CodexResetTray.Tests/
```

The app intentionally avoids Electron, browser runtimes, local databases, and background services. WPF plus a WinForms `NotifyIcon` keeps the resident app small and native on Windows.

## Privacy

Codex Reset Tray does not read `C:\Users\<you>\.codex\auth.json`. Treat that file like a password vault. The app reads rate limits through Codex's app-server protocol and redacts token-shaped data from error text before showing it.

More detail: [PRIVACY.md](PRIVACY.md)

## Safety Model

The app must never consume reset credits. It has no UI button for that behavior and no generic RPC sender. Tests assert that startup requests do not contain reset, auth, logout, or consume methods.

More detail: [docs/rate-limit-source.md](docs/rate-limit-source.md)

## Contributing

Bug reports and PRs are welcome. Please keep the project lightweight, native, and read-only by default. See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

MIT. See [LICENSE](LICENSE).
