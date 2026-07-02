# Codex Reset Tray

[![CI](https://github.com/dklianev/codex-reset-tray/actions/workflows/ci.yml/badge.svg)](https://github.com/dklianev/codex-reset-tray/actions/workflows/ci.yml)
[![Latest release](https://img.shields.io/github/v/release/dklianev/codex-reset-tray?label=release)](https://github.com/dklianev/codex-reset-tray/releases/latest)
[![License: MIT](https://img.shields.io/badge/license-MIT-2ea043.svg)](LICENSE)
[![Windows](https://img.shields.io/badge/windows-10%20%2F%2011-0078d4.svg)](README.md)
[![.NET](https://img.shields.io/badge/.NET-10-512bd4.svg)](https://dotnet.microsoft.com/)

Codex Reset Tray is a lightweight Windows tray app for watching Codex rate-limit windows without keeping a browser open.

It shows the 5-hour and weekly limits, reset credit count, exact local reset times, smart refresh status, and optional per-credit expiry metadata in a native tray-first UI.

Built for: **Windows 10/11**, **WPF**, **.NET 10**, **read-only Codex app-server access**, and a small resident footprint.

## Why This Exists

Codex rate limits are easy to forget until they matter. This app keeps the useful information one click away:

- How much of the 5-hour window is left.
- How much of the weekly window is left.
- When each window resets, both relative and exact local time.
- How many manual reset credits are available.
- When reset credits expire, if you opt into the experimental expiry lookup.

The app is intentionally boring in the important places: no Electron shell, no telemetry, no local database, no background Windows service, and no generic RPC console.

## Feature Highlights

| Area | What You Get |
| --- | --- |
| Tray icon | Dual-ring signal: outer 5-hour capacity, inner weekly capacity, rendered as a crisp multi-resolution icon. |
| Tray hover | Compact structured summary with remaining percentages and reset timing. |
| Tray menu | Exact reset times, reset credits, refresh, dashboard, notification mute, Start with Windows, and Exit. |
| Dashboard | Modern frameless WPF dashboard with twin gauges, reset timeline, notification center, settings, and status states. |
| Refresh | Smart cadence: 5 minutes normally, 1 minute under 15 minutes to reset, 30 seconds under 2 minutes. |
| Notifications | Optional low-remaining alerts, reset-credit added alerts, and direct rate-limit reset alerts. |
| Privacy | Uses `codex app-server --listen stdio://` by default and does not read `.codex/auth.json` unless you enable expiry lookup. |
| Packaging | Self-contained portable `win-x64` zip, produced by `packaging/publish.ps1`. |
| Tests | Parser, redaction, refresh cadence, tray icon, startup, notifications, and packaging regression coverage. |

## Data Sources

Default source:

```powershell
codex app-server --listen stdio://
```

The app sends only the read-only startup flow plus `account/rateLimits/read`.

Optional source:

```text
GET https://chatgpt.com/backend-api/wham/rate-limit-reset-credits
```

This reset-credit expiry lookup is experimental, disabled by default, and used only to display per-credit expiry dates. It reads the local Codex auth file at refresh time only after you opt in. The official app-server reset credit count remains the source of truth.

More detail: [docs/rate-limit-source.md](docs/rate-limit-source.md)

## Install

Download the latest `CodexResetTray-win-x64.zip` from [GitHub Releases](https://github.com/dklianev/codex-reset-tray/releases/latest), extract it, and run:

```text
CodexResetTray.exe
```

Requirements for the portable app:

- Windows 10 or Windows 11, 64-bit.
- Codex CLI installed and available on `PATH`.
- A signed-in Codex account in the normal Codex environment.

The release build is self-contained, so users do not need to install the .NET runtime separately.

Note: until the app is code-signed, Windows SmartScreen may warn on first launch. That is expected for unsigned open-source binaries.

To verify a downloaded release zip, compare the release note checksum with:

```powershell
Get-FileHash .\CodexResetTray-win-x64.zip -Algorithm SHA256
```

## Usage

On launch, the app opens the dashboard and adds a system tray icon.

- Close the dashboard window to keep the tray app running.
- Use the tray menu to reopen the dashboard, refresh manually, mute notifications, toggle Start with Windows, or exit.
- Hover the tray icon for a compact live summary.
- Enable reset-credit expiry lookup only if you want per-credit expiry dates.

Percentages are shown as **remaining capacity**, not used capacity. A direct reset notification is shown when a limit reaches 100% remaining before the previously scheduled reset time. A reset-credit notification is shown only when the available credit count increases.

## Runtime Behavior

Manual launches show the dashboard and add the tray icon. Closing the dashboard hides it; it does not exit the app. Use Exit from the dashboard footer or tray menu to stop it.

When started by Windows, the app launches minimized to the tray. Launching a second copy activates the existing instance instead of starting another background process.

## Settings And Startup

Alert and expiry settings are stored per user at:

```text
%APPDATA%\CodexResetTray\settings.json
```

Defaults:

- Windows and in-app notifications are enabled.
- Low-capacity alerts are enabled at 10% remaining.
- Reset-credit expiry lookup is disabled.
- Reset-credit expiry warnings use a 48-hour warning window.

Use the tray menu to mute all notifications, change low-alert thresholds, enable or disable expiry lookup, and toggle Start with Windows.

Start with Windows uses the current user's registry Run key:

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
```

It stores the current executable path with `--minimized` and does not require administrator rights. If you move the portable app folder, toggle Start with Windows off and on again so the saved path is refreshed.

## Update And Uninstall

To update, exit the tray app, download the newest release zip, and replace the extracted app folder. Settings stay in `%APPDATA%\CodexResetTray\settings.json`.

To uninstall, exit the app, turn off Start with Windows if enabled, delete the extracted app folder, and optionally delete `%APPDATA%\CodexResetTray`.

## Development

Install the .NET 10 SDK. Release scripts also use PowerShell 7+ (`pwsh`).

For day-to-day development:

```powershell
dotnet restore .\CodexResetTray.slnx
dotnet build .\CodexResetTray.slnx -c Release
dotnet test .\CodexResetTray.slnx -c Release --no-build
dotnet run --project .\src\CodexResetTray.App\CodexResetTray.App.csproj
```

The app project targets `net10.0-windows` and uses WPF for the dashboard plus WinForms `NotifyIcon` for native tray integration.

## Build A Portable Release

```powershell
pwsh -NoProfile -File .\packaging\publish.ps1
```

The release zip is written to:

```text
artifacts\release\CodexResetTray-win-x64.zip
```

For the full maintainer flow:

```powershell
pwsh -NoProfile -File .\packaging\verify-release.ps1
```

That runs restore, build, tests, packaging failure-handling verification, and publish.

More detail: [docs/release.md](docs/release.md)

## Project Layout

```text
src/
  CodexResetTray.Core/
    Display/        reset and percent formatting
    Protocol/       app-server request creation and response parsing
    RateLimits/     tolerant domain models
    Security/       secret redaction helpers
    Startup/        Windows startup command formatting
  CodexResetTray.App/
    Assets/         app and tray icon assets
    Controls/       custom WPF gauge control
    Resources/      theme tokens and shared styles
    Services/       Codex source, tray, settings, startup, expiry lookup
    ViewModels/     dashboard state, commands, notifications
tests/
  CodexResetTray.Tests/
packaging/
  publish.ps1
  verify-release.ps1
  generate-app-icon.ps1
docs/
  architecture.md
  rate-limit-source.md
  release.md
```

More detail: [docs/architecture.md](docs/architecture.md)

## Privacy And Safety

Codex Reset Tray is read-only by design:

- It has no reset-credit consume button.
- It has no generic app-server RPC sender.
- It does not collect prompts, outputs, repository contents, or telemetry.
- It does not persist access tokens, account IDs, or raw expiry endpoint responses.
- It redacts token-shaped values and local auth paths before showing errors.

The app does not read `C:\Users\<you>\.codex\auth.json` by default. It reads that file only if you enable experimental reset-credit expiry lookup, and only to make the narrow metadata request needed for expiry dates.

If expiry lookup is enabled, the app reads `tokens.access_token` and `tokens.account_id` from `%CODEX_HOME%\auth.json` when `CODEX_HOME` is set, otherwise from `%USERPROFILE%\.codex\auth.json`. Those values are sent only as request headers to the expiry metadata endpoint and are not stored by this app.

More detail: [PRIVACY.md](PRIVACY.md) and [SECURITY.md](SECURITY.md)

## Troubleshooting

`Tray icon is missing`

Check the Windows tray overflow menu. Windows may hide new tray icons behind the `^` overflow button until you pin them.

`Codex CLI not found`

Make sure `codex` is available in a new PowerShell window:

```powershell
codex --version
```

If you installed or updated Codex while the app was already running, exit Codex Reset Tray and start it again so it gets the refreshed `PATH`.

`Unsupported app-server` or `account/rateLimits/read failed`

Update Codex CLI and try Refresh from the tray menu. The app depends on the Codex app-server rate-limit read method.

`Notifications do not appear`

Check Windows notification settings, Focus Assist / Do Not Disturb, and the app's tray-menu notification mute. Low-capacity alerts also require the selected threshold to be enabled.

`Expiry lookup unavailable`

The optional WHAM endpoint is unofficial and may change. The app should continue showing the official rate-limit windows and reset credit count even when expiry metadata fails.

`Published exe is locked`

Exit the tray app before running `packaging/publish.ps1`. The publish script refuses to overwrite a running published executable.

## Contributing

Bug reports and PRs are welcome. Please keep changes aligned with the project goals:

- Native Windows behavior.
- Read-only by default.
- Small resident footprint.
- Defensive parsing of Codex responses.
- Clear user-facing degraded states instead of crashes.
- Tests for parser, safety, notification, packaging, and formatting changes.

See [CONTRIBUTING.md](CONTRIBUTING.md).

## Status

This is a pre-1.0 project. Codex app-server fields and the optional reset-credit expiry endpoint may change, so parser tolerance and clear fallback behavior are part of the design.

Current limitations:

- Windows-only; there is no macOS or Linux tray build.
- Portable zip only; no installer or auto-updater yet.
- Release builds are unsigned, so Windows SmartScreen may warn on first launch.
- Requires a Codex CLI version that supports `codex app-server --listen stdio://` and `account/rateLimits/read`.
- The reset-credit expiry lookup uses an unofficial ChatGPT endpoint and may change or stop working.
- Windows notifications depend on system notification settings, Focus Assist / Do Not Disturb, and tray notification behavior.

## License

MIT. See [LICENSE](LICENSE).
