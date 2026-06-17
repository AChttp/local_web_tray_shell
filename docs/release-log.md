# Release Log

This file records the actual publishing workflow used for this repository, including repository bootstrap, versioning, tagging, and GitHub Release publication.

## 2026-06-17 - v1.0.3 Title Bar Double-Click Release

### Versioning and Release Preparation

1. Updated release version file:
   - `VERSION`
   - Current value: `1.0.3`
2. Updated release documentation:
   - `README.md`
   - `CHANGELOG.md`
   - `docs/releases/v1.0.3.md`
3. Rebuilt the application with:
   - `powershell -ExecutionPolicy Bypass -File .\build.ps1`
4. Verified bundled dependencies and startup checks with:
   - `dist\Switch.exe --self-test`
5. Release preparation commit:
   - Commit: `d62d460`
   - Message: `Prepare v1.0.3 release`

### Tagging

1. Created annotated Git tag:
   - `v1.0.3`
2. Pushed the tag to GitHub:
   - `origin v1.0.3`

### GitHub Release Publication

1. Created GitHub Release for tag:
   - `v1.0.3`
2. Release URL:
   - `https://github.com/AChttp/local_web_tray_shell/releases/tag/v1.0.3`
3. Uploaded release assets:
   - `Switch.exe`
   - `Switch-v1.0.3-win-x64.zip`

### Release Artifact Hashes

- `Switch.exe`
  - SHA256: `02C5E010CD1F95252F15C77914C1418E68E9598CA8377C3A36DE7F6C0802896E`
- `Switch-v1.0.3-win-x64.zip`
  - SHA256: `9E7C736F09B4E91AEAB61C41E908541965EA7EC8F5A3ED058006EC0C83D8B5DE`

### Notes

- This release fixes custom title bar double-click behavior while preserving title bar dragging.
- GitHub CLI was not available on the machine, so the GitHub Release was created through the GitHub REST API using the existing Git credential helper configuration and the local proxy.

## 2026-06-16 - v1.0.2 Web Navigation Release

### Versioning and Release Preparation

1. Updated release version file:
   - `VERSION`
   - Current value: `1.0.2`
2. Updated release documentation:
   - `README.md`
   - `CHANGELOG.md`
   - `docs/releases/v1.0.2.md`
3. Rebuilt the application with:
   - `powershell -ExecutionPolicy Bypass -File .\build.ps1`
4. Verified bundled dependencies and startup checks with:
   - `dist\Switch.exe --self-test`
5. Release preparation commit:
   - Commit: `ca5bbfd`
   - Message: `Prepare v1.0.2 release`

### Tagging

1. Created annotated Git tag:
   - `v1.0.2`
2. Pushed the tag to GitHub:
   - `origin v1.0.2`

### GitHub Release Publication

1. Created GitHub Release for tag:
   - `v1.0.2`
2. Release URL:
   - `https://github.com/AChttp/local_web_tray_shell/releases/tag/v1.0.2`
3. Uploaded release assets:
   - `Switch.exe`
   - `Switch-v1.0.2-win-x64.zip`

### Release Artifact Hashes

- `Switch.exe`
  - SHA256: `D90F8DE8C3D97345C7CCA943C3591C88F51AC3CEC85516B1EA26C7924AC65693`
- `Switch-v1.0.2-win-x64.zip`
  - SHA256: `80954245E94E554028702A106DEFAF7DDE8D407B9606EFEE8B37C61A66E1C1DE`

### Notes

- This release includes taskbar minimize behavior for the borderless window and embedded WebView navigation improvements.
- GitHub CLI was not available on the machine, so the GitHub Release was created through the GitHub REST API using the existing Git credential helper configuration and the local proxy.

## 2026-06-15 - v1.0.1 Sidebar and Log Performance Release

### Versioning and Release Preparation

1. Updated release version file:
   - `VERSION`
   - Current value: `1.0.1`
2. Updated release documentation:
   - `README.md`
   - `CHANGELOG.md`
   - `docs/releases/v1.0.1.md`
3. Rebuilt the application with:
   - `powershell -ExecutionPolicy Bypass -File .\build.ps1`
4. Verified bundled dependencies and startup checks with:
   - `dist\Switch.exe --self-test`
5. Release preparation commit:
   - Commit: `98d3000`
   - Message: `Prepare v1.0.1 release`

### Tagging

1. Created annotated Git tag:
   - `v1.0.1`
2. Pushed the tag to GitHub:
   - `origin v1.0.1`

### GitHub Release Publication

1. Created GitHub Release for tag:
   - `v1.0.1`
2. Release URL:
   - `https://github.com/AChttp/local_web_tray_shell/releases/tag/v1.0.1`
3. Uploaded release assets:
   - `Switch.exe`
   - `Switch-v1.0.1-win-x64.zip`

### Release Artifact Hashes

- `Switch.exe`
  - SHA256: `C576C0B5B537CA6D628A8E2E8A3DD312A7806DD91C27C846977CA38FC3765B12`
- `Switch-v1.0.1-win-x64.zip`
  - SHA256: `1877669632F4A37D620788B762A9ADEA4E2F8D1BFA374A3DEE67417E6BE21229`

### Notes

- This release includes the sidebar owner-draw cleanup, real-time sidebar/workspace drag resizing improvements, and throttled command log UI refresh.
- GitHub CLI was not available on the machine, so the GitHub Release was created through the GitHub REST API using the existing Git credential helper configuration.

## 2026-06-13 - v1.0.0 First Public Release

### Repository Bootstrap

1. Initialized a local Git repository in `local_web_tray_shell`
2. Added open source repository files:
   - `README.md`
   - `.gitignore`
   - `LICENSE`
3. Created the initial open source commit:
   - Commit: `20641d9`
   - Message: `Initial open source release`
4. Renamed the default branch from `master` to `main`
5. Added GitHub remote:
   - `origin = https://github.com/AChttp/local_web_tray_shell.git`
6. Pushed `main` to GitHub

### Versioning and Release Preparation

1. Added release version file:
   - `VERSION`
   - Current value: `1.0.0`
2. Added release history file:
   - `CHANGELOG.md`
3. Added release notes file:
   - `docs/releases/v1.0.0.md`
4. Updated `build.ps1` to:
   - Read version from `VERSION`
   - Generate assembly version metadata
   - Produce `dist\Switch.exe`
   - Produce `dist\Switch-v1.0.0-win-x64.zip`
5. Rebuilt the application with:
   - `build.cmd`
6. Release preparation commit:
   - Commit: `9963b3c`
   - Message: `Prepare v1.0.0 release`
7. Pushed the release preparation commit to `origin/main`

### Tagging

1. Created annotated Git tag:
   - `v1.0.0`
2. Pushed the tag to GitHub:
   - `origin v1.0.0`

### GitHub Release Publication

1. Created GitHub Release for tag:
   - `v1.0.0`
2. Release URL:
   - `https://github.com/AChttp/local_web_tray_shell/releases/tag/v1.0.0`
3. Uploaded release assets:
   - `Switch.exe`
   - `Switch-v1.0.0-win-x64.zip`

### Release Artifact Hashes

- `Switch.exe`
  - SHA256: `4E19D118D490D5A437F588F494FD71BA10A5C0041BCB7EE3752994BE724B3DE9`
- `Switch-v1.0.0-win-x64.zip`
  - SHA256: `5475923234CFDCEBFD787A1E79731E32D1FF5119D1E32C6C6622FE037F4BC3FD`

### Notes

- Git push required a safe-directory override for this machine:
  - `git -c safe.directory=C:/Users/Hakeem/Desktop/Project/local_web_tray_shell push`
- GitHub CLI was not available on the machine, so the GitHub Release was created through the GitHub REST API using the existing Git credential helper configuration.
