# Changelog

All notable changes to this project will be documented in this file.

## v1.0.1 - 2026-06-15

Maintenance release focused on sidebar responsiveness and log rendering performance.

### Changed

- Reworked sidebar resizing so the left control surface and right workspace resize together during drag.
- Replaced the old multi-control sidebar rendering path with a single owner-drawn sidebar surface.
- Removed obsolete sidebar list/card controls that were no longer visible after the owner-drawn sidebar migration.
- Batched command runtime UI refreshes to reduce cross-thread UI work during high-frequency command output.
- Made log rendering append new lines incrementally when possible instead of rewriting the whole log text box.
- Increased the maximum sidebar width to support wider left panels and smaller embedded web views.

### Fixed

- Reduced flicker and broken intermediate layout states while resizing the sidebar.
- Improved behavior when command logs update rapidly while the logs view is open.

### Release Assets

- `Switch.exe`
- `Switch-v1.0.1-win-x64.zip`

## v1.0.0 - 2026-06-13

First public open source release.

### Added

- Windows desktop shell for local web apps based on `WinForms + WebView2`
- Multi-site support with an always-visible site list in the left sidebar
- Independent web views for different local sites, including built-in defaults for `8080` and `8099`
- Command management for `Direct` / `cmd` / `PowerShell`
- Command start, stop, auto-start, auto-retry, and runtime log viewing
- Workspace switching between web pages and command logs
- System tray integration with restore, refresh, auto-start, stop-all, and exit actions
- Collapsible left control panel with persistent multi-pane workspace
- Chinese UI labels and a custom Switch icon
- Single-file `Switch.exe` packaging with embedded WebView2 dependencies

### Changed

- Reworked the left sidebar layout to reduce clipping and overlap issues
- Converted command and site action bars to a stable single-row button layout
- Improved command/site card rendering and reduced list flicker

### Release Assets

- `Switch.exe`
- `Switch-v1.0.0-win-x64.zip`
