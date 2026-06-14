# Repository Guidelines

## Project Structure & Module Organization
`src/` contains the full C# WinForms application, including `Program.cs`, UI forms, command/process management, configuration storage, and embedded dependency bootstrapping. `docs/` holds release notes and publication history. Top-level files such as `README.md`, `CHANGELOG.md`, and `VERSION` describe the current release. Build outputs are written to `dist/`, temporary compiler files to `obj/`, and the generated app icon to `assets/switch.ico`.

## Build, Test, and Development Commands
- `build.cmd` - Windows entry point for the release build.
- `powershell -ExecutionPolicy Bypass -File .\build.ps1` - Runs the build directly.
- `dist\Switch.exe` - Starts the compiled app after a successful build.
- `dist\Switch.exe --self-test` - Verifies bundled dependencies and startup checks.

The build script compiles the app with `csc.exe`, generates `Switch.exe`, and packages a release ZIP in `dist/`.

## Coding Style & Naming Conventions
Use standard C# style: 4-space indentation, braces on their own lines, PascalCase for types and members, and camelCase for local variables and parameters. Keep internal helper classes and enums in `namespace LocalWebTrayShell`. Prefer explicit, descriptive names that match the existing code, such as `CommandManager`, `RunModeCatalog`, and `AppConfigStore`. Do not hand-edit generated files under `dist/`, `obj/`, or `assets/switch.ico`.

## Testing Guidelines
There is no dedicated automated test project yet. Validate changes by rebuilding, launching `dist\Switch.exe`, and running `--self-test` when dependency loading or packaging changes are involved. For command/process changes, verify start, stop, restart, and auto-retry behavior against a local site or command entry.

## Commit & Pull Request Guidelines
Git history is short and uses plain imperative summaries, for example `Initial open source release` and `Prepare v1.0.0 release`. Keep commit subjects concise and action-oriented. Pull requests should explain what changed, how it was verified, and include screenshots or screen recordings only for visible UI changes. Link related issues or release notes when relevant.

## Configuration Notes
Runtime settings are stored in `%LocalAppData%\SwitchShell\switch-config.json`. The app expects WebView2 Runtime to be installed on the machine, even though WebView2 DLLs are bundled into the executable.
