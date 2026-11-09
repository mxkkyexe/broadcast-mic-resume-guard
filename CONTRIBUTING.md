# Contributing

Build with `scripts/Build.ps1` on 64-bit Windows. All policy tests must pass. Keep COM audio callbacks nonblocking: copy the notification into the bounded queue and return; perform file I/O and COM work on the application thread. Do not add `SetMute` calls or volume enforcement loops.

Before claiming a release is validated, exercise this matrix on a real Windows installation:

| Scenario | Expected result |
| --- | --- |
| Broadcast running, level 73, suspend/resume resets to zero | One restoration to 73 after about five seconds |
| Pre-sleep level zero | No restoration |
| Pre-sleep mute; Silence mute during recovery | No restoration or unmute |
| User changes to a positive level during recovery | User level remains |
| No sleep, or login with zero | No write |
| Broadcast closed during sleep | No write |
| Missing, ambiguous, inactive or recreated endpoint | Fail closed; log reason |
| Repeat sleep cycles and duplicate resume messages | One decision per cycle |
| Observation mode | Logs only; no write |
| Install, login, tray exit, restart, uninstall | One normal-user process; startup removed cleanly |

Do not post raw logs without checking them for device IDs or personal paths. No physical microphone is required for policy tests. Never simulate sleep or change a contributor's real microphone without their consent.
