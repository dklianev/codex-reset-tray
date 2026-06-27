# Release Guide

## Local Checks

```powershell
dotnet test .\CodexResetTray.slnx
dotnet build .\CodexResetTray.slnx -c Release
.\packaging\publish.ps1
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
4. Create a GitHub release with the zip from `artifacts/release`.
