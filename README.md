# Nova Battery

A small Windows 10/11 tray app for the **GameSir Nova 2 Lite**. It reads the
controller's own vendor HID battery report over either the included 2.4 GHz
receiver or a USB-C cable. No driver or GameSir Connect process is required.

## Run it

1. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
   on a Windows PC.
2. Double-click `build-windows.cmd`.
3. Open `dist\NovaBattery.exe`.

The produced EXE is self-contained, so the computer that runs it does not need
.NET installed. Windows may show a SmartScreen warning because the local build
is not code-signed.

Alternatively, push this folder to GitHub. The included **Build Windows EXE**
workflow compiles it on a Windows runner. Download `NovaBattery-Windows-x64`
from the workflow run's **Artifacts** section and extract `NovaBattery.exe`.

## Features

- Automatically finds GameSir vendor HID interfaces, including known Nova 2
  Lite product IDs `3537:1098` and `3537:100F`.
- Works with the 2.4 GHz receiver and scans the wired USB interface too.
- Refreshes every 15 seconds and survives unplug/reconnect cycles.
- Tray icon and low-battery notification.
- Remembers the last numeric level when wired firmware reports only “charging”.
- `--once` command for scripts and `--probe` diagnostics for firmware variants.
- Optional “Start with Windows” setting (current user only).

## Command line

```text
NovaBattery.exe            Open the window and tray icon
NovaBattery.exe --once     Print one reading and exit
NovaBattery.exe --probe    List matching HID collections and raw reply frames
```

If the controller is not detected, close GameSir Connect temporarily and run
`NovaBattery.exe --probe` in Command Prompt. The probe is read-only apart from
the normal battery query and is useful when a firmware update changes a PID.

## Battery protocol

The controller exposes a vendor collection on HID usage page `0xFF7A`. Nova
Battery writes a 65-byte output report (`00 01 01`, then zeros), ignores
unsolicited `F4 00` state frames, and reads the battery field at byte 19 from a
`00 01 ...` reply. `0xFF` means charging; values from 0 to 100 are percentages.

GameSir firmware can report “charging” without an exact percentage during a
wired session. In that case Nova Battery clearly labels the value as the last
known wireless reading.

## Privacy and safety

Everything runs locally. The app makes no network requests and only sends the
documented battery query to GameSir HID interfaces. It does not change firmware
or controller settings.
