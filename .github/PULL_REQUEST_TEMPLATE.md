## Summary

- TBD

## Checklist

- [ ] Targeting `main`.
- [ ] Build passes: `dotnet build .\CodexResetTray.slnx -c Release`.
- [ ] Tests pass: `dotnet test .\CodexResetTray.slnx -c Release --no-build`.
- [ ] Release-impacting changes pass: `pwsh -NoProfile -File .\packaging\verify-release.ps1`.
- [ ] Parser, safety, formatting, notification, or packaging changes have focused tests.
- [ ] New Codex RPC methods or authenticated endpoints are documented and justified as read-only.
- [ ] No secrets, generated binaries, local `bin/obj`, logs, or Codex auth files are committed.

## Security / Privacy Impact

- [ ] No new data source or credential access.
- [ ] New data source or credential access is documented in `PRIVACY.md` and `SECURITY.md`.
- [ ] Not applicable.

## Screenshots

Add screenshots for dashboard, tray menu, or notification UI changes.
