# Broadcast Mic Resume Guard

An experimental Windows community workaround for an NVIDIA Broadcast virtual microphone that resets its Windows recording level to zero after sleep/resume.

The guard remembers the actual level before sleep and conditionally restores it **once**, five seconds after resume. It never unmutes a device and does not continuously force volume to 100.

**Status:** initial portable implementation. A predecessor PowerShell guard was successfully tested by one user with NVIDIA Broadcast 2.3.0.13104 on Windows 11. The compiled version still needs real sleep/resume validation across machines. This is not an NVIDIA product or a confirmed fix for an internal vendor bug. Restart/login resets are not repaired.

## What it does

- Automatically finds the active Broadcast recording endpoint from Windows metadata. If none or more than one match is active, it waits without writing.
- Reads endpoint level, mute, and state every 500 ms; listens for volume/mute notifications and Windows suspend/resume messages.
- Requires an active, unmuted, positive pre-sleep level sampled within three seconds of suspend, with Broadcast running.
- Makes at most one decision per observed sleep cycle, between five and thirty seconds after resume.
- Cancels recovery on observed mute, a positive volume change, endpoint errors/changes, or notification loss.
- Rechecks the live endpoint immediately before writing its saved level. Never calls `SetMute`.
- Runs as your normal user with a tray icon and no console. No service, administrator rights, driver edits, or network access in the application.

## Build and try it

Requires 64-bit Windows 10/11 with .NET Framework 4.8 or newer. Windows 11 includes it. No NuGet packages or separate SDK required; the build uses the Windows .NET Framework compiler. Other Windows releases and Modern Standby behavior are not yet validated.

Download or clone the source, then from Windows PowerShell in the project folder:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build.ps1
```

The build runs the policy tests and writes the application, installer, removal script, and SHA-256 checksums to `dist`.

Double-click `dist\BroadcastMicResumeGuard.exe` to run until you exit it from its tray menu. To observe events without making any volume changes:

```powershell
.\dist\BroadcastMicResumeGuard.exe --observe
```

Do not run alongside another volume-restoration tool. Exit any existing copy before switching between observation and normal modes; the second launch exits rather than changing the first copy's mode.

To start automatically at login, run the installer **from the built `dist` directory**:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\dist\Install.ps1
```

It copies the application to `%LOCALAPPDATA%\Programs\BroadcastMicResumeGuard` and creates a shortcut in your Startup folder. `-NoStart` installs without launching immediately. It refuses to install over the legacy scheduled-task guard by default; remove that version first. For a reversible migration, stop and disable the legacy task, then use `-AllowDisabledLegacyTask` to keep that disabled task for rollback. The installer also checks for a running legacy `Guard.ps1` process. Building and running diagnostics does not replace an existing installation.

To remove it, run the installed `Uninstall.ps1` or the copy in `dist`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\dist\Uninstall.ps1
```

This stops the compiled guard, removes its startup shortcut and installed application files, and retains diagnostic logs. It leaves microphone settings as they are. Exit from the tray menu only stops the current session; the Startup shortcut remains until uninstall.

## Diagnostics and privacy

Use the tray menu to open `%LOCALAPPDATA%\BroadcastMicResumeGuard`. Logs contain timestamps, endpoint identifiers, levels, mute/device state, and whether Broadcast is running. No audio samples, credentials, or other process command lines are collected. `events.log` retains one backup, approximately 2 MB combined. Startup errors have a separate log.

Create a read-only endpoint report:

```powershell
$report=Join-Path $env:TEMP 'broadcast-mic-diagnostics.txt'
$p=Start-Process .\dist\BroadcastMicResumeGuard.exe -ArgumentList @('--diagnose', ('"'+$report+'"')) -PassThru -Wait
Get-Content $report
```

Review reports before posting them: endpoint IDs can identify a device installation. Notification context GUIDs help correlate changes but **do not identify the process that changed the level**. The program has no telemetry, auto-updater, or remote control.

## Mute behavior and limits

Silence and similar tools should use the Windows endpoint mute flag. A mute observed before sleep or during recovery cancels restoration, even if it is subsequently cleared during that recovery cycle. The installed version of each third-party mute tool must be tested independently.

An intentional **unmuted zero volume** during the five-second recovery window cannot be distinguished from the reported bug. Do not rely on that method of muting with this workaround. Windows provides no atomic compare-and-set of mute and volume; a concurrent mute/volume change can race a restoration. This app never clears mute, but it cannot promise that no simultaneous change will occur.

Transient endpoint failures cancel the cycle for safety. Device recreation, late resets after the single decision, missed power broadcasts, sleep while the guard is stopped, and a restart/login reset may therefore require manual recovery. Logs can establish timing; they do not prove an NVIDIA root cause.

## Security and development

The executable runs with the permissions of whoever launches it; do not run it elevated. Installation uses a per-user Startup shortcut. Store it where untrusted users cannot edit its files. Releases are currently unsigned; build from source if you prefer to inspect the code. ExecutionPolicy Bypass in the example is process-scoped for build/install scripts, not a system-wide change; the installed guard does not use PowerShell or VBScript.

The core policy is tested without touching a microphone. Native endpoint discovery, notification subscription, actual power delivery, and installer behavior require Windows integration testing. See [CONTRIBUTING.md](CONTRIBUTING.md) for the manual validation matrix.

Reference behavior: [Windows power broadcasts](https://learn.microsoft.com/windows/win32/power/wm-powerbroadcast), [nonblocking audio notification callbacks](https://learn.microsoft.com/windows/win32/api/endpointvolume/nn-endpointvolume-iaudioendpointvolumecallback).

MIT licensed. NVIDIA and NVIDIA Broadcast are trademarks of their respective owner; this project is unaffiliated.
