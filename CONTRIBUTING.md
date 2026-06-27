# Contributing

Thanks for helping make Codex Reset Tray better.

## Development

```powershell
dotnet restore .\CodexResetTray.slnx
dotnet test .\CodexResetTray.slnx
dotnet build .\CodexResetTray.slnx -c Release
```

## Design Principles

- Prefer native Windows behavior over heavy runtimes.
- Keep the app read-only and privacy-preserving.
- Parse Codex app-server responses defensively; tolerate missing and extra fields.
- Do not hardcode rate-limit IDs beyond choosing a preferred display bucket.
- Keep UI states explicit: loading, ready, near limit, limited, unavailable, and error.

## Pull Request Checklist

- Tests cover parser, safety, or formatting changes.
- `dotnet test` passes locally.
- `dotnet build -c Release` passes with no warnings.
- New Codex RPC methods are documented and justified.
- No secrets, generated binaries, or local `bin/obj` output are committed.
