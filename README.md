# MX Backlight Control

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D6.svg)](#requirements)

A native Windows application (C# / .NET 9 WPF + Windows HID API) built from scratch to control the keyboard backlight of Logitech MX series keyboards (MX Keys, MX Keys S, MX Keys Mini, MX Mechanical, etc.) in **Always On**, **Always Off**, or **Auto (Default)** modes.

## Features

- **Native HID++ 2.0 / 4.5 Engine:** Communicates directly with the keyboard via Windows `setupapi.dll`, `hid.dll`, and `kernel32.dll` P/Invoke — no external drivers or third-party HID libraries required.
- **3 Backlight Control Modes:**
  - **Always On:** Disables power-saving / ambient sensor shutoff and uses a configurable keep-alive loop to prevent the backlight from fading out.
  - **Always Off:** Locks the backlight off at the hardware level; hand proximity sensors and keystrokes will not turn it back on, even after sleep or reconnect.
  - **Auto (Default):** Restores Logitech's default proximity and ambient light sensor behavior.
- **Broad Connectivity Support:** Works over Bluetooth LE (`0xFF43`), Logi Bolt (`0xC548`), Unifying (`0xC52B`), and direct USB connections.
- **Live Hardware Telemetry:** Displays keyboard model name, connection type, battery percentage & charging state, live brightness level, and HID++ protocol version.
- **System Tray & Global Hotkey:** Runs quietly in the background; switch modes anytime from the tray menu or with `Ctrl + Alt + L`.
- **English & Turkish UI:** Instant live language switching (`EN` / `TR` button in the title bar) with automatic OS language detection and persistent settings.
- **Launch at Windows Startup:** Optional automatic startup minimized to the system tray.

## Download

Download the latest pre-built Windows executable (`.exe`) from the [Releases](https://github.com/emrekrtp/logi_mx/releases/latest) page:

- **`MxBacklightControl-v1.0.0-win-x64-standalone.exe`** — Self-contained single-file executable (no .NET installation required).
- **`MxBacklightControl-v1.0.0-win-x64.exe`** — Lightweight single-file executable (requires [.NET 9.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)).

## Requirements

- Windows 10 / 11
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (to build from source)

## Build & Run

```powershell
dotnet build MxBacklightControl.sln -c Release
```

Compiled executable path:
`MxBacklightControl\bin\Release\net9.0-windows\MxBacklightControl.exe`

To run directly:
```powershell
dotnet run --project MxBacklightControl\MxBacklightControl.csproj -c Release
```

## Contributing

Contributions are welcome! Please check [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines on submitting issues and pull requests.

## License

This project is open source and licensed under the [MIT License](LICENSE).
