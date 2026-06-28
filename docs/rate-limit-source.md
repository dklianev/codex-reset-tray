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

## Experimental Reset-Credit Expiry

Codex app-server currently provides the reset credit count, but not per-credit expiry dates. Codex Reset Tray can optionally enrich the dashboard with expiry metadata from:

```text
GET https://chatgpt.com/backend-api/wham/rate-limit-reset-credits
```

This is disabled by default. When enabled, the app reads only these fields from the local Codex auth file:

- `tokens.access_token`
- `tokens.account_id`

The request sends:

- `Authorization: Bearer <access token>`
- `ChatGPT-Account-ID: <account id>`
- `OpenAI-Beta: codex-1`
- `originator: Codex Desktop`

The parser accepts only:

- `available_count`
- `credits[].title`
- `credits[].status`
- `credits[].reset_type`
- `credits[].granted_at`
- `credits[].expires_at`

The endpoint is unofficial and may change. Failures are ignored and the official app-server snapshot stays visible. The endpoint's `available_count` is never used to override `rateLimitResetCredits.availableCount` from app-server.

## Why Not auth.json By Default

`auth.json` contains sensitive local account material. A tray monitor should not open it, parse it, cache it, or ask the user to paste it. Codex app-server already exposes the read-only account metadata needed for this use case.

The expiry lookup is a narrow opt-in exception because per-credit expiry is not currently exposed by the app-server snapshot.
