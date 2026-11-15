# Battery Monitor

A Windows tray utility that shows the remaining battery level of wireless mice, keyboards,
controllers and headsets.

Devices are detected automatically — no manual setup. Ships as a single executable that runs
directly on any Windows 10 / 11 machine, with no .NET runtime required.

[中文](README.zh.md)

## Features

- **Automatic detection**: whatever you plug in shows up; no configuration needed when you swap devices
- **Tray battery**: the icon shows a ring gauge for the first device in the list; hovering lists the top 3, one per line
- **Controls**: left click to refresh, double click to open the panel, right click for the menu
- **Drag to reorder**: drag device rows up and down; the order is saved. Put the device you care about on top and the tray follows
- **Low battery alerts**: a Windows notification fires when any device drops below 20%, once per device, re-armed after charging or reconnecting
- **Adjustable refresh interval**; optionally minimize to tray instead of closing
- **Launch at startup**: in administrator mode a scheduled task starts it elevated with no UAC prompt
- **Battery colors**: ≥60% green, 30–59% yellow, <30% red
- Reads everything locally — **no network access, no log files**

## Supported devices

Each cell shows the ID a device or receiver appears as, and its status:
![Verified](https://img.shields.io/badge/Verified-2ea44f) reads on real hardware,
![Untested](https://img.shields.io/badge/Untested-dfb317) implemented but not yet tried,
![Not supported](https://img.shields.io/badge/Not%20supported-9e9e9e) not supported.

### ASUS ROG

One ROG OMNI receiver can carry a keyboard and a mouse at the same time; they are split into
two rows. The pairing ID is the model number reported in the receiver's pairing list, which is
not the same as the USB ID the device uses when plugged in by cable.

#### Keyboards

| Device | Receiver | Pairing ID | Status |
| --- | --- | --- | --- |
| ROG Strix Scope II 96 RX | `0B05:1ACE` | `0x1B7A` | ![Verified](https://img.shields.io/badge/Verified-2ea44f) |
| ROG Strix Scope II 96 | `0B05:1ACE` | `0x1AB0` | ![Untested](https://img.shields.io/badge/Untested-dfb317) |
| ROG Azoth | `0B05:1ACE` | `0x1A85` | ![Untested](https://img.shields.io/badge/Untested-dfb317) |
| ROG Azoth Extreme | `0B05:1ACE` | `0x1B42` | ![Untested](https://img.shields.io/badge/Untested-dfb317) |
| ROG Azoth Extreme SE | `0B05:1ACE` | `0x1CF1` | ![Untested](https://img.shields.io/badge/Untested-dfb317) |
| ROG Falchion RX Low Profile | `0B05:1ACE` | `0x1B06` | ![Untested](https://img.shields.io/badge/Untested-dfb317) |

#### Mice

| Device | Receiver | Pairing ID | Status |
| --- | --- | --- | --- |
| ROG Keris II Ace | `0B05:1ACE` | `0x1B1A`, `0x1B18` | ![Verified](https://img.shields.io/badge/Verified-2ea44f) |
| ROG Gladius III Aimpoint | `0B05:1ACE` | `0x1A72` | ![Untested](https://img.shields.io/badge/Untested-dfb317) |
| ROG Harpe Ace Aim Lab Edition | `0B05:1ACE` | `0x1A94` | ![Untested](https://img.shields.io/badge/Untested-dfb317) |
| ROG Harpe Ace Extreme | `0B05:1ACE` | `0x1B68`, `0x1B69` | ![Untested](https://img.shields.io/badge/Untested-dfb317) |
| ROG Harpe Ace Mini | `0B05:1ACE` | `0x1B65` | ![Untested](https://img.shields.io/badge/Untested-dfb317) |
| ROG Keris II Origin | `0B05:1ACE` | `0x1C0E` | ![Untested](https://img.shields.io/badge/Untested-dfb317) |
| ROG Keris II Origin KJP | `0B05:1ACE` | `0x1D4E` | ![Untested](https://img.shields.io/badge/Untested-dfb317) |
| ROG Keris Wireless Aimpoint | `0B05:1ACE` | `0x1A68`, `0x1A6A` | ![Untested](https://img.shields.io/badge/Untested-dfb317) |
| ROG Strix Impact III Wireless | `0B05:1ACE` | `0x1AD7` | ![Untested](https://img.shields.io/badge/Untested-dfb317) |

### Razer

| Device | USB ID | Status |
| --- | --- | --- |
| Razer Viper Ultimate | `1532:007B` | ![Verified](https://img.shields.io/badge/Verified-2ea44f) |
| Razer Mouse Dock | `1532:007E` | ![Not supported](https://img.shields.io/badge/Not%20supported-9e9e9e) |

Razer needs administrator rights; at normal rights this row asks you to restart as administrator.

### Controllers

| Device | USB ID | Status |
| --- | --- | --- |
| Flydigi VADER4 (Xbox-compatible mode) | `045E:028E` | ![Verified](https://img.shields.io/badge/Verified-2ea44f) |
| Other XInput controllers | `045E:028E` | ![Untested](https://img.shields.io/badge/Untested-dfb317) |

Controllers only report 5 levels, less precise than a vendor protocol.

### SteelSeries

| Device | USB ID | Status |
| --- | --- | --- |
| SteelSeries Arctis 7 | `1038:12AD` | ![Verified](https://img.shields.io/badge/Verified-2ea44f) |
| Other Arctis wireless headsets, including Arctis Pro | `1038:1260`, `1038:1252`, `1038:1280` | ![Untested](https://img.shields.io/badge/Untested-dfb317) |

While the headset is asleep the receiver still answers, but the battery byte reads 0. Nudge
the headset and refresh.

### Bluetooth

Keyboards, mice and controllers switched to Bluetooth go through the standard Bluetooth
battery service, independent of any vendor protocol and without a receiver. Headsets do not
use this path.

| Device | USB ID | Status |
| --- | --- | --- |
| ROG Strix Scope II 96 RX WL | `0B05:1B7B` | ![Verified](https://img.shields.io/badge/Verified-2ea44f) |
| Other Bluetooth keyboards, mice and controllers | — | ![Untested](https://img.shields.io/badge/Untested-dfb317) |

## Usage

### Getting the program

Download `BatteryMonitor.exe` from [Releases](https://github.com/smartwsw/UniversalBatteryMonitor/releases)
and run it, or build it from source:

```powershell
.\build.ps1              # build dist\BatteryMonitor.exe
.\build.ps1 -Clean       # wipe bin / obj / dist first
.\build.ps1 -Run         # build then launch
```

The default target is a self-contained `win-x64` single file (about 71 MB). Pass
`-Runtime win-arm64` or `-Runtime win-x86` for other architectures.

### Command line

| Argument | Effect |
|---|---|
| none | Open the panel |
| `--tray` | Start minimized to the tray (used by launch-at-startup) |
| `--once` | Read once from the console and exit, no window |
| `--list` | List every HID interface on the machine, for troubleshooting |
| `--version` | Print the version |

### The panel

Change the refresh interval, toggle launch-at-startup, and choose whether closing the window
minimizes to the tray. The window opens at the bottom-right of the screen by default, and the
device list uses a thin dark scrollbar that supports both the wheel and dragging.

## Notes

- **Razer requires administrator**: the receiver's control interface is only open to elevated
  processes, so on a normal launch that row suggests restarting as administrator. The tray
  right-click menu has "Restart as administrator"; the program never elevates itself, which
  would pop a UAC prompt at logon. The Razer Mouse Dock is out of scope.
- **Controllers are coarse**: XInput only reports 5 levels (empty / low / medium / full / unknown),
  mapped to 15 / 40 / 70 / 100% — less precise than vendor protocols. A controller plugged in
  by cable still reports a level and is labeled "wired".
- **Zero while asleep**: the ROG OMNI receiver still answers while the device is asleep, but the
  battery byte is 0. Nudge the device and refresh; the UI says so instead of reporting a false 0%.
- **ROG OMNI model names**: the receiver identifies itself only as `ROG OMNI RECEIVER`; the real
  model comes from the pairing info. Unknown models appear as `ROG Device 0xXXXX` until they are
  added to the model table.
