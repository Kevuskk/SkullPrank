#!/usr/bin/env bash
# Requires Mono (mcs). Produces a single self-contained Skull.exe.
set -e
cd "$(dirname "$0")"

if [ ! -f assets/skel.gif ]; then
  echo "[!] assets/skel.gif is missing." >&2
  exit 1
fi

# Restart is ON by default. Add -define:PRANK_SAFE to disable it.
# Add -define:PRANK_LOOP to repeat the finale instead of exiting.
mcs -sdk:4.5 -target:winexe -optimize+ \
  -r:System.dll \
  -r:System.Core.dll \
  -r:System.Drawing.dll \
  -r:System.Windows.Forms.dll \
  -resource:assets/skel.gif,skel.gif \
  -resource:assets/sounds/shutdown.wav,shutdown.wav \
  -resource:assets/sounds/fakeencrypted.wav,fakeencrypted.wav \
  -resource:assets/sounds/glitch.wav,glitch.wav \
  -win32icon:assets/skull.ico \
  -out:Skull.exe \
  src/Program.cs

echo "[OK] Built ./Skull.exe"
