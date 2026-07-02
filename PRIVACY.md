# Privacy

Codex Reset Tray is designed to show rate-limit reset times with the smallest practical data surface.

## What It Reads

- It starts `codex app-server --listen stdio://`.
- It sends `initialize`, `initialized`, and `account/rateLimits/read`.
- It reads the returned rate-limit buckets and reset credit count.
- If the experimental reset-credit expiry lookup is enabled, it reads `tokens.access_token` and `tokens.account_id` from `%CODEX_HOME%\auth.json` when `CODEX_HOME` is set, otherwise from `%USERPROFILE%\.codex\auth.json`, for one HTTPS metadata request to ChatGPT. It then displays only per-credit expiry dates/status text.

## What It Does Not Read

- It does not open `auth.json` unless the experimental reset-credit expiry lookup is enabled.
- It does not read Codex session logs.
- It does not collect prompts, outputs, repository contents, or file paths beyond sanitized error messages.
- It does not send telemetry to this project or to any third-party service.
- It does not persist access tokens, account IDs, or raw expiry endpoint responses.
- It does not expose raw endpoint field names or response bodies in the dashboard, tray menu, notifications, or settings.

## Local Process Behavior

During refresh, the app starts a short Codex app-server session, reads the current rate-limit snapshot, then closes the process. The WPF tray app remains running between refreshes.

When reset-credit expiry lookup is enabled, the app makes a short `GET https://chatgpt.com/backend-api/wham/rate-limit-reset-credits` request after the official app-server snapshot succeeds. If that request fails, the dashboard keeps the official app-server data and marks expiry metadata unavailable.

## Error Redaction

If Codex returns an error, the app redacts common bearer token, JWT, API key, and Windows user path patterns before displaying the message.
