# SkullPrank

A harmless Windows prank payload. Fake "Windows Security" alerts, fake Microsoft Edge
notifications, red-skull flashes, a fake ransomware screen built from the target's own
filenames and a screenshot of their desktop — and a skeleton that occasionally sprints
across the screen.

Nothing is encrypted, deleted, modified, or transmitted. No persistence, no registry
writes, no network. When it exits (or the machine restarts), everything is back to normal.

> **This is a prank, not malware.** It is built to *look* alarming but does not touch
> your data.

---

## Read this first

- **Only run it on a machine you own or have explicit permission to prank.**
- The finale is loud, full-screen and deliberately scary. Do **not** use it on someone
  with a heart condition, photosensitivity, or severe anxiety — and never on a work
  machine you do not control.
- The default build **forces a restart** at the end of the finale. Build with `PRANK_SAFE`
  to keep it visual-only; note that a restart closes apps and can lose unsaved work.
- No warranty. You are responsible for what you run and where you run it.

---

## Features

- Fake "Windows Security — 3 threats found" dialog
- Two red-skull console windows
- 4-second blackout
- 12 rotating fake Microsoft notifications (Edge, Rewards, OneDrive, Update, Xbox, Teams, Bing, M365, Security)
- Random skull flash with a synthesized beep
- A real `cmd.exe` window showing a fake `del /s` transcript using actual filenames
- Fake ransomware: description, live encrypted-file counter, 72-hour timer, desktop screenshot as "proof"
- Shutdown sting, glitch effect, and a recorded voice-over around the finale
- Fake BSOD that climbs to 99% and restarts the machine
- Fake BIOS POST
- Skeleton GIF that runs across the screen during calm moments
- One-shot browser open and a `dir /s` terminal gag

Everything is reversible. Mouse buttons, taskbar and Caps Lock are restored on exit.

---

## Requirements

- Windows 10 / 11 (32- or 64-bit)
- .NET Framework 4.5+ (built into Windows 10/11)
- No administrator rights

---

## Build

The animation and the skull icon are embedded into the executable, so the output is a
single file.

**Windows** (uses the `csc.exe` that ships with .NET Framework):

```bat
build.bat
```

**Linux / macOS** (Mono `mcs`):

```sh
./build.sh
```

Both produce `Skull.exe` in the repo root.

### Disabling the real restart

The default build ends the fake BSOD at 99% and restarts the machine. To keep the finale
visual-only, build with the `PRANK_SAFE` symbol:

```bat
:: csc
csc /define:PRANK_SAFE ...
```

```sh
# mcs
mcs -define:PRANK_SAFE ...
```

Everything else is identical.

### Repeating the finale

By default the finale runs once: the machine restarts (or, with `PRANK_SAFE`, the program
exits). To make it loop forever instead, build with `PRANK_LOOP`:

```sh
mcs -define:PRANK_LOOP ...
```

---

## Run

Double-click `Skull.exe`.

Windows SmartScreen will warn you because the binary is unsigned:
**More info → Run anyway**. Some antivirus engines may flag it on behaviour heuristics
(full-screen overlays, synthesized audio, spawning `cmd.exe`). Whitelist the folder if
you intend to test it.

---

## Stop

Run `STOP.bat`. It signals the payload, waits for its cleanup, then kills it if it is
still alive and removes the temporary files.

You can also just reboot — nothing survives a restart.

---

## Configuration

All of it lives at the top of `src/Program.cs`:

| Constant | Meaning |
|---|---|
| `QuietSeconds` | quiet period after the intro |
| `GapMin` / `GapMax` | seconds between random events |
| `SkullPercent` | how often an event is a skull flash |
| `NotifPercent` | cumulative threshold for notifications; the rest is intrusive effects |
| `FirstFinaleSec` | when the scripted finale starts |
| `FinaleEverySec` | how often the finale repeats |
| `SilenceAfterSec` | silence after a finale |
| `BrowserDelaySec` | when the browser opens once |
| `TerminalDelaySec` | delay after the browser before the `dir /s` window |

Finale step durations are the literal arguments in `RunFinale()`.

---

## How it works

- **WinForms + GDI+** for every overlay, dialog and notification.
- The skull is drawn with `GraphicsPath`/`FillEllipse`, not an image.
- The beep is a WAV synthesized in memory and played through `winmm!PlaySound`.
- The "deletion" phase spawns a real `cmd.exe` that `type`s a pre-generated transcript —
  which is why it looks like a genuine console.
- The skeleton is an embedded GIF stepped manually with `SelectActiveFrame`.
- Audio (the beep plus the two clips) is embedded as WAV and played through
  `winmm!PlaySound`.
- Intrusive effects use `SwapMouseButton`, `ShowWindow` on `Shell_TrayWnd`, and `keybd_event`.

---

## License

MIT — see [LICENSE](LICENSE).

The skeleton GIF in `assets/` is included for the demo, as are the audio clips in
`assets/sounds/`. Make sure you have the right to redistribute any animation or audio you
swap in.
