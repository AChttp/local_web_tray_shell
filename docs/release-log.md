# Release Log

This file records the actual publishing workflow used for this repository, including repository bootstrap, versioning, tagging, and GitHub Release publication.

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
