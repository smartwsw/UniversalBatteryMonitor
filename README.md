# Battery Monitor

A Windows tray utility that shows the remaining battery level of wireless mice, keyboards,
controllers, and headsets.

Devices are detected automatically — no manual setup. Ships as a single executable that runs
directly on any Windows 10 / 11 machine, with no .NET runtime required.

[中文](README.md)

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

Only devices actually tested on this machine are listed below.

### Verified working

| Device | Connection | Result |
|---|---|---|
| ROG Strix Scope II 96 RX | ROG OMNI receiver | Reads, keyboard channel |
| ROG Keris II Ace | ROG OMNI receiver | Reads, mouse channel |
| Flydigi VADER4 (Xbox-compatible mode) | USB receiver | Reads, 5-level precision |
| Razer Viper Ultimate | 2.4G receiver | Reads, requires administrator |

One ROG OMNI receiver carrying both a keyboard and a mouse is split into two rows.

### Detected but no battery reading

| Device | Symptom |
|---|---|
| Razer Mouse Dock | Interface does not support battery queries; excluded from the supported list |
| SteelSeries Arctis 7 | Receiver responds, but the battery byte reads 0 while the headset is asleep |

### Other devices

The SteelSeries Arctis family (including Arctis Pro) is supported, but only the Arctis 7 was
tested here. Devices without a built-in protocol still get a standard HID battery field lookup —
if it can be read, it is shown; otherwise the row states the specific reason.

## Usage

### Getting the program

Download `BatteryMonitor.exe` from [Releases](https://github.com/smartwsw/UniversalBatteryMonitor/releases)
and run it, or build it from source:

```powershell
.\build.ps1              # build dist\BatteryMonitor.exe
.\build.ps1 -Clean       # wipe bin / obj / dist first
.\build.ps1 -Run         # build then launch
```

The default target is a self-contained `win-x64` single file (about 62 MB). Pass
`-Runtime win-arm64` or `-Runtime win-x86` for other architectures.

### Command line

| Argument | Effect |
|---|---|
| none | Open the panel |
| `--tray` | Start minimized to the tray (used by launch-at-startup) |
| `--once` | Read once from the console and exit, no window |
| `--list` | List every HID interface on the machine, for troubleshooting |

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
- **Zero while asleep**: the ROG OMNI and SteelSeries receivers still answer while the device is
  asleep, but the battery byte is 0. Nudge the device and refresh; the UI says so instead of
  reporting a false 0%.
- **ROG OMNI model names**: the receiver identifies itself only as `ROG OMNI RECEIVER`; the real
  model comes from the pairing info. Unknown models appear as `ROG Device 0xXXXX` until they are
  added to the model table.
