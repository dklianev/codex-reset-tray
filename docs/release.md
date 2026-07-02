# Release Guide

## Repository

- Suggested GitHub repository name: `codex-reset-tray`.
- Default branch: `main`.
- Tag format: `v0.1.0`, `v0.1.1`, and so on.
- Release notes should be based on `CHANGELOG.md`.

## Local Checks

Release scripts use PowerShell 7+ (`pwsh`).

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

Tagged GitHub releases also publish:

```text
artifacts/release/CodexResetTray-win-x64.zip.sha256
```

The checksum is attached to the GitHub release and included in the release notes.

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
4. Commit and push `main`.
5. Create a signed or annotated tag such as `v0.1.0` from `main`.
6. Push the tag.

The GitHub Actions release job validates that the tag matches the project version and that the changelog contains the same release section. It then builds the portable zip, creates the `.sha256` file, and creates the GitHub release. The job refuses to overwrite an existing release for the same tag.

Example:

```powershell
git tag -a v0.1.0 -m "Release v0.1.0"
git push origin v0.1.0
```
