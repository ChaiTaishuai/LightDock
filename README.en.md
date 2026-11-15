# Light Dock

[简体中文](README.md) | English

Light Dock is a lightweight Windows desktop dock with hover magnification, translucent styling and desktop icon management. The current mainline version is **0.3.0**.

## Features

- Up to 12 top-level icons, expandable groups, scrolling and drag-and-drop ordering.
- Files, folders, shortcuts, image previews and Recycle Bin integration with Shell event updates.
- Double-click the desktop background to hide or restore the dock, desktop icons and taskbar.
- Hover information cards with configurable delay; per-monitor DPI layout.
- Simplified Chinese, English or system language; optional current-user startup at sign-in.
- Restore managed desktop icons on exit, with a recovery watchdog for unexpected termination.

## Installation

Windows 11 x64, or Windows 10 1607+ x64 with .NET Framework 4.8. Extract the entire release archive into a permanent user-writable folder and run `bin/LightDock.exe`. Version 0.1.0 uses `bin/AsterDock.exe`. Keep the configuration file beside the executable. Do not run inside the ZIP. Settings are created in `data/settings.xml` beside the `bin` folder.

Exit the running version before starting another. Back up settings before upgrading. Enable startup again after moving the application. To uninstall, disable startup, exit through the tray menu to restore the desktop, then remove the extracted folder.

## Build and validation

Run `./build.ps1` in Windows PowerShell. The build uses the Windows .NET Framework x64 C# compiler and WPF references. See `docs/RELEASE-AUDIT.md` for the seven-version inventory and validation limits.

All seven source snapshots compiled successfully during release preparation. Existing test reports are historical evidence, not a new interactive acceptance run. Other computers, mixed-DPI displays, actual sign-out/sign-in and antivirus compatibility remain unverified. These builds are unsigned; SHA-256 checksums identify files and do not certify publisher identity.

## Versions

Chronological development sequence: 0.1.0 → 0.2.0-beta.1 → beta.2 → beta.3 → beta.4 → 0.3.0-beta.1 → 0.3.0. Beta releases are experimental. Historical binaries may show 0.0.0.0 internally; archive names and release manifests identify their documented version.

No license has been selected in this workspace. Publication does not itself grant an open-source license.
