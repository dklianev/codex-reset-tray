# Release Guide

## Local Checks

```powershell
pwsh -NoProfile -File .\packaging\verify-release.ps1
```

## Artifact

The release script creates:

```text
artifacts/
  publish/win-x64/
    CodexResetTray.exe
  release/
    CodexResetTray-win-x64.zip
```

`packaging\publish.ps1` publishes into a fresh staging folder, checks native command exit codes, and only replaces the previous publish output and release zip after a new archive is complete. It refuses to continue if the previous published `CodexResetTray.exe` is still running from that folder. The final line prints the exact zip path to upload.

## Notes For Users

- The app is unsigned unless a maintainer signs the executable.
- Windows SmartScreen may warn on first launch for unsigned builds.
- Users need Codex CLI installed and available on `PATH`.
- The app does not modify Codex rate limits or consume reset credits.

## Versioning

Before a tagged release:

1. Update `Version` in `src/CodexResetTray.App/CodexResetTray.App.csproj`.
2. Update `CHANGELOG.md`.
3. Run local checks.
4. Create a GitHub release with `artifacts\release\CodexResetTray-win-x64.zip`.
