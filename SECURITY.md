# Security Policy

## Supported Versions

This project is pre-1.0. Security fixes target the latest `main` branch until release branches exist.

## Reporting A Vulnerability

Open a private security advisory on GitHub if the repository has advisories enabled. Otherwise, open an issue with a minimal reproduction and avoid posting secrets, tokens, screenshots of `auth.json`, or full Codex logs.

## Security Boundaries

Codex Reset Tray is read-only by design:

- No `.codex/auth.json` reads.
- No reset-credit consume methods.
- No generic app-server RPC console.
- No telemetry.
- Token-shaped errors are redacted before display.

Changes that add new Codex RPC methods should include tests proving they are read-only and should document the new data surface.
