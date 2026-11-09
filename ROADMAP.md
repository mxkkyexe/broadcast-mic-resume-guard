# Initial development roadmap

- [x] Portable endpoint discovery with ambiguity protection
- [x] Compiled tray application with normal-user startup and removal
- [x] Resume-only policy and mute cancellation tests
- [x] Read-only endpoint/notification-subscription smoke test on Windows
- [x] Synthetic power-window message test
- [ ] Real sleep/resume validation of the compiled build (the predecessor script has one successful user report)
- [ ] Install/login/uninstall validation in a clean Windows account
- [ ] Windows 10/11 and Modern Standby compatibility reports
- [ ] Measure steady-state CPU/memory across several systems
- [ ] Signed release packaging after integration validation

Startup/login resets remain a separate investigation. No automatic login repair is planned without a trustworthy baseline and a way to preserve intentional mute and zero-volume choices.
