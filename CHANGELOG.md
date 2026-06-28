# Changelog

## 0.1.0

- Initial Windows tray app.
- WPF dashboard with 5-hour and weekly reset windows.
- Read-only Codex app-server integration.
- Reset credit count display.
- Core parser and redaction tests.
- Refined cockpit-style dashboard and dynamic tray percentage display.
- Polished the dashboard visual system and replaced the tray tile badge with a transparent progress-ring icon.
- Redesigned the dashboard into a cleaner Windows 11 Fluent visual system: refined palette, tabular numerics, clearer hierarchy, and a calmer hero card.
- Rebuilt the system tray icon as a true multi-resolution usage gauge (16-64 px) that stays crisp at every DPI and legible on light and dark taskbars.
- Added a branded multi-resolution application/window icon (`Assets/app.ico`) with a reproducible generator (`packaging/generate-app-icon.ps1`).
- Unified status colors across the dashboard and tray into one usage-state ramp (emerald -> amber -> orange -> red); blue is reserved for the weekly window.
- Hardened refresh, docs-link, and exit handling so transient failures surface in the UI instead of crashing the resident app.
- Reworked the dashboard into a modern frameless Windows 11 app: custom rounded chrome, gradient-mesh backdrop, twin radial usage gauges with state-driven gradient strokes and glow, monospace data readouts, and a staggered load animation.
- Reworked the system tray icon into a dual-signal mark: 5-hour outside, weekly inside, with a compact 16 px rendering for taskbar legibility.
- Refreshed the application/window icon to a matching emerald-to-cyan ring mark.
- Added configurable low-remaining alerts, manual reset credit notifications, and direct reset detection.
- Added an in-app notification center with a title-bar bell, unread badge, and compact alert history.
- Added a persisted global notification mute in the tray menu.
- Improved tray hover text and tray menu reset details, including exact local reset times.
- Added experimental opt-in reset-credit expiry metadata with tray/dashboard status, expiry warnings, and strict fallback to official app-server counts.
- Added smart auto-refresh cadence: 5 minutes normally, 1 minute near reset, and 30 seconds when a reset is imminent.
