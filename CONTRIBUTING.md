# Contributing

Thanks for helping make Codex Reset Tray better.

## Development

```powershell
dotnet restore .\CodexResetTray.slnx
dotnet build .\CodexResetTray.slnx -c Release
dotnet test .\CodexResetTray.slnx -c Release --no-build
```

Release scripts use PowerShell 7+ (`pwsh`). Before opening a release-oriented PR, run:

```powershell
pwsh -NoProfile -File .\packaging\verify-release.ps1
```

## Design Principles

- Prefer native Windows behavior over heavy runtimes.
- Keep the app read-only and privacy-preserving.
- Parse Codex app-server responses defensively; tolerate missing and extra fields.
- Do not hardcode rate-limit IDs beyond choosing a preferred display bucket.
- Keep UI states explicit: loading, ready, near limit, limited, unavailable, and error.

## Pull Request Checklist

- Target the `main` branch.
- Tests cover parser, safety, or formatting changes.
- `dotnet build -c Release` passes with no warnings.
- `dotnet test .\CodexResetTray.slnx -c Release --no-build` passes locally.
- Release-impacting changes pass `pwsh -NoProfile -File .\packaging\verify-release.ps1`.
- New Codex RPC methods are documented and justified.
- No secrets, generated binaries, or local `bin/obj` output are committed.
