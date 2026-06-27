# Rate-Limit Data Source

The source of truth is Codex app-server over stdio:

```powershell
codex app-server --listen stdio://
```

The app writes JSONL requests to stdin and reads JSONL responses from stdout.

## Outbound Methods

Allowed:

- `initialize`
- `initialized`
- `account/rateLimits/read`

Not allowed:

- Any method containing `reset`
- Any reset-credit consume method
- Any auth, logout, or token export method
- Generic user-entered RPC methods

## Parsed Fields

The parser tolerates missing and extra fields. Current known fields include:

- `rateLimits`
- `rateLimitsByLimitId`
- `rateLimitResetCredits.availableCount`
- `primary.usedPercent`
- `primary.windowDurationMins`
- `primary.resetsAt`
- `secondary.usedPercent`
- `secondary.windowDurationMins`
- `secondary.resetsAt`

The app treats `resetsAt` as a Unix timestamp returned by Codex and converts it to local display time.

## Why Not auth.json

`auth.json` contains sensitive local account material. A tray monitor should not open it, parse it, cache it, or ask the user to paste it. Codex app-server already exposes the read-only account metadata needed for this use case.
