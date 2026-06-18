# AetherLink ⚡

> **Stealth USB Reverse Tethering** — Route Android device traffic through a corporate network invisibly via ADB.

## What is AetherLink?

AetherLink is a cross-platform desktop tool (.NET 10 + MAUI) that creates a transparent proxy chain between an Android device and a corporate network:

```
Android System Proxy  →  ADB Reverse Tunnel  →  Local Relay (tcp:8888)  →  Corporate Proxy  →  Internet
```

This allows an Android device to access the internet through a host machine's corporate network connection without any VPN or MDM configuration on the device itself.

---

## Features

- 🔌 **One-click ADB reverse tethering** — full proxy injection into Android system settings
- 🏢 **Corporate proxy chaining** — auto-detects Windows system proxy + env vars, or manual override
- 🖥 **PC Terminal Launcher** — spawns PowerShell with `HTTP_PROXY` / `HTTPS_PROXY` pre-injected
- 📦 **Portable ADB** — bundled `adb.exe` + DLLs, no Android SDK install required
- 🏗 **Clean Architecture** — MAUI MVVM · SRP · DI · CommunityToolkit.Mvvm

---

## Architecture

```
AetherLink/
├── AetherLink.Core/          # Class library (net10.0)
│   ├── Abstractions/         # Interfaces: IAndroidDeviceService, IAdbTunnelService,
│   │                         #             IProxyRelayService, ITerminalLauncherService
│   ├── Models/               # AndroidDevice (record), DeviceStatus (enum)
│   └── Services/             # Concrete implementations
│
└── AetherLink.UI/            # MAUI App (Windows + macOS)
    ├── ViewModels/           # MainViewModel (ObservableObject + RelayCommands)
    ├── Views/                # MainPage.xaml (dark theme, 4-card layout)
    ├── Converters/           # InverseBoolConverter
    ├── MauiProgram.cs        # DI composition root
    └── adb/                  # Bundled adb.exe + AdbWinApi.dll + AdbWinUsbApi.dll
```

---

## Prerequisites

| Tool | Minimum Version |
|---|---|
| .NET SDK | 10.0 |
| MAUI Workload | `dotnet workload install maui` |
| Visual Studio | 2022 v17.14+ (or Rider) |

**Fonts** (place in `AetherLink.UI/Resources/Fonts/`):
- [Inter](https://fonts.google.com/specimen/Inter) — Regular, SemiBold, Bold
- [JetBrains Mono](https://www.jetbrains.com/lp/mono/) — Regular

---

## Getting Started

```powershell
# Clone
git clone https://github.com/DanielWueno/AetherLink.git
cd AetherLink

# Restore & build
dotnet workload install maui
dotnet restore
dotnet build

# Run on Windows
dotnet run --project AetherLink.UI/AetherLink.UI.csproj -f net10.0-windows10.0.19041.0
```

---

## How It Works

1. **Scan** — Queries the ADB server (starts it if needed from bundled binary) for connected devices.
2. **Connect** — Starts the local TCP relay on `127.0.0.1:8888`, creates an `adb reverse` forward, and injects Android system proxy settings.
3. **Android traffic** flows: `Android → tcp:8888 → relay → corporate proxy → internet`
4. **Terminal Launcher** — Optionally spawns PowerShell with proxy env vars for CLI tools on the host PC.
5. **Disconnect** — Clears Android proxy settings, removes ADB reverse forward, stops relay.

---

## License

MIT — see [LICENSE](LICENSE) for details.
