# Changelog

All notable changes to this project will be documented in this file.

## v1.0.4 - 2026-06-25

Maintenance release focused on tray/window responsiveness and sidebar usability.

### Added

- Global hotkey to show or hide the main window from anywhere (default `Ctrl + ``, disabled by default; configurable via the tray menu).
- Move up / move down controls for command and site list items, with order persisted across restarts.

### Fixed

- Restoring the window from the tray is now near-instant; removed the window handle recreation that previously caused a ~2s delay while the embedded WebView2 re-attached.
- Dragging the title bar of a maximized window now restores it to normal size and follows the pointer, matching standard Windows behavior.
- Fixed the command/site section titles ("命令" / "站点") having their bottom pixels clipped.
- Fixed the sidebar lists going blank or not refilling after switching the window between normal and maximized.
- Fixed list item text bleeding past the list edge (into the section header) when scrolled, because GDI text ignored the list clip region.

### Release Assets

- `Switch.exe`
- `Switch-v1.0.4-win-x64.zip`

## v1.0.3 - 2026-06-17

Maintenance release focused on title bar window behavior.

### Fixed

- Fixed the custom title bar double-click behavior so the window can switch from normal mode to maximized mode and back consistently.
- Preserved title bar dragging by starting the system move operation only after the pointer crosses the drag threshold.

### Release Assets

- `Switch.exe`
- `Switch-v1.0.3-win-x64.zip`

## v1.0.2 - 2026-06-16

Maintenance release focused on embedded web navigation and desktop window behavior.

### Added

- Added site-level web navigation actions for returning to the previous page and returning to the configured home URL.
- Added lightweight navigation history fallback for embedded WebView pages when WebView2 history state is not immediately available.

### Changed

- Links that request a new window now open inside the current embedded WebView instead of spawning a separate window.
- Moved web navigation actions into the site section so they are grouped with the selected site.

### Fixed

- Fixed borderless window taskbar behavior so clicking the taskbar icon can minimize the foreground window.
- Fixed failed navigation messages so they show the actual target URL instead of the configured site home URL.

### Release Assets

- `Switch.exe`
- `Switch-v1.0.2-win-x64.zip`

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
