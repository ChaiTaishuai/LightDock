# Light Dock

[简体中文](README.md) | English

**Keep everyday shortcuts close and your desktop clear.**

Light Dock is a lightweight Windows desktop launcher for apps, files, folders and the Recycle Bin. Keep frequent items in a primary row and organize additional material in expandable groups. Drag to organize, scroll to browse, and collapse the groups when you are finished.

A crowded desktop makes familiar files harder to find; putting every shortcut in a single row makes the launcher unwieldy. Light Dock separates everyday favorites from grouped material you only need on demand. Original files stay in place, and managed desktop icons return when you exit.

[Download latest](https://github.com/ChaiTaishuai/LightDock/releases/latest) · [All releases](https://github.com/ChaiTaishuai/LightDock/releases)

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

## Version history

`main` shows the current mainline. Use the source links below or select **Tags** in the branch selector to inspect historical files. Historical tags preserve their original contents; expanded guides are maintained on the current mainline.

|Version|Focus|Guide|Source|Download|
|---|---|---|---|---|
|0.1.0|Single-level stable baseline|[Guide](docs/versions/v0.1.0.md)|[Source](https://github.com/ChaiTaishuai/LightDock/tree/v0.1.0)|[Download](https://github.com/ChaiTaishuai/LightDock/releases/tag/v0.1.0)|
|0.2.0-beta.1|Expandable groups|[Guide](docs/versions/v0.2.0-beta.1.md)|[Source](https://github.com/ChaiTaishuai/LightDock/tree/v0.2.0-beta.1)|[Download](https://github.com/ChaiTaishuai/LightDock/releases/tag/v0.2.0-beta.1)|
|0.2.0-beta.2|Recycle Bin and drag-and-drop|[Guide](docs/versions/v0.2.0-beta.2.md)|[Source](https://github.com/ChaiTaishuai/LightDock/tree/v0.2.0-beta.2)|[Download](https://github.com/ChaiTaishuai/LightDock/releases/tag/v0.2.0-beta.2)|
|0.2.0-beta.3|Event-driven Recycle Bin refresh|[Guide](docs/versions/v0.2.0-beta.3.md)|[Source](https://github.com/ChaiTaishuai/LightDock/tree/v0.2.0-beta.3)|[Download](https://github.com/ChaiTaishuai/LightDock/releases/tag/v0.2.0-beta.3)|
|0.2.0-beta.4|Hover information cards|[Guide](docs/versions/v0.2.0-beta.4.md)|[Source](https://github.com/ChaiTaishuai/LightDock/tree/v0.2.0-beta.4)|[Download](https://github.com/ChaiTaishuai/LightDock/releases/tag/v0.2.0-beta.4)|
|0.3.0-beta.1|Chinese and English UI|[Guide](docs/versions/v0.3.0-beta.1.md)|[Source](https://github.com/ChaiTaishuai/LightDock/tree/v0.3.0-beta.1)|[Download](https://github.com/ChaiTaishuai/LightDock/releases/tag/v0.3.0-beta.1)|
|0.3.0|Current stable mainline|[Guide](docs/versions/v0.3.0.md)|[Source](https://github.com/ChaiTaishuai/LightDock/tree/v0.3.0)|[Download](https://github.com/ChaiTaishuai/LightDock/releases/tag/v0.3.0)|


No license has been selected. Publication does not itself grant an open-source license.
