# Security Policy

## Supported Versions

This project is pre-1.0. Security fixes target the latest `main` branch until release branches exist.

## Reporting A Vulnerability

Open a private security advisory on GitHub if the repository has advisories enabled. Otherwise, open an issue with a minimal reproduction and avoid posting secrets, tokens, screenshots of `auth.json`, or full Codex logs.

## Security Boundaries

Codex Reset Tray is read-only by design:

- No `.codex/auth.json` reads unless the user explicitly enables experimental reset-credit expiry lookup.
- No reset-credit consume methods.
- No generic app-server RPC console.
- No telemetry.
- Token-shaped errors are redacted before display.

Experimental reset-credit expiry lookup is metadata-only. It reads the local Codex auth file at refresh time, sends one allowlisted HTTPS request, displays non-clickable expiry rows, does not persist credentials or raw responses, adds no reset/consume action, and never overrides the official app-server reset credit count.

Changes that add new Codex RPC methods or new authenticated endpoints should include tests proving they are read-only and should document the new data surface.
