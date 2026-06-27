# Privacy

Codex Reset Tray is designed to show rate-limit reset times with the smallest practical data surface.

## What It Reads

- It starts `codex app-server --listen stdio://`.
- It sends `initialize`, `initialized`, and `account/rateLimits/read`.
- It reads the returned rate-limit buckets and reset credit count.

## What It Does Not Read

- It does not open `.codex/auth.json`.
- It does not read Codex session logs.
- It does not collect prompts, outputs, repository contents, or file paths beyond sanitized error messages.
- It does not send telemetry to this project or to any third-party service.

## Local Process Behavior

During refresh, the app starts a short Codex app-server session, reads the current rate-limit snapshot, then closes the process. The WPF tray app remains running between refreshes.

## Error Redaction

If Codex returns an error, the app redacts common bearer token, JWT, API key, and Windows user path patterns before displaying the message.
