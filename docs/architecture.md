# Architecture

Codex Reset Tray has two projects: a pure core library and a Windows UI app.

## Core

`CodexResetTray.Core` contains code that can be tested without WPF:

- `Protocol/AppServerProtocol.cs` creates the minimal app-server JSONL startup messages.
- `Protocol/AppServerRateLimitParser.cs` parses current and future-tolerant rate-limit payloads.
- `RateLimits/*` stores parsed buckets and windows.
- `Display/*` formats relative/exact reset times and remaining-percent copy.
- `Security/SecretRedactor.cs` redacts token-shaped values before they reach the UI.

The parser prefers `rateLimitsByLimitId` because it supports multiple buckets. It falls back to the legacy `rateLimits` shape when needed.

## App

`CodexResetTray.App` is a WPF app with a WinForms `NotifyIcon`.

- `App.xaml.cs` owns single-instance startup, the tray controller, and the 10-minute refresh timer.
- `Services/CodexAppServerRateLimitSource.cs` runs a short read-only Codex app-server session.
- `Services/TrayController.cs` owns the Windows tray icon, context menu, and Windows notifications.
- `Services/JsonAlertSettingsService.cs` persists lightweight per-user alert settings.
- `ViewModels/DashboardViewModel.cs` maps snapshots and errors into UI state, detects alert-worthy changes, and keeps in-app notification history.
- `MainWindow.xaml` renders the dashboard and the compact notification center.

## Why WPF

The app is Windows-only and tray-first. WPF avoids Electron, a bundled browser, a local web server, and heavy runtime dependencies. WinForms is used only for `NotifyIcon`, because WPF has no native tray icon component.

## Refresh Strategy

The current implementation opens a short app-server session per refresh:

1. Start `codex app-server --listen stdio://`.
2. Send `initialize`.
3. Send `initialized`.
4. Send `account/rateLimits/read`.
5. Parse the response.
6. Close the child process.

This keeps the resident app lightweight while avoiding direct auth-file access. A future version can switch to a long-lived app-server process and subscribe to `account/rateLimits/updated` if real-time updates become important.
