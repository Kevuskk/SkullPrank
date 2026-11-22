#!/usr/bin/env bash
# Requires Mono (mcs). Produces a single self-contained Skull.exe.
set -e
cd "$(dirname "$0")"

if [ ! -f assets/skel.gif ]; then
  echo "[!] assets/skel.gif is missing." >&2
  exit 1
fi

# Add -define:PRANK_REBOOT to let the fake BSOD actually restart the machine.
mcs -sdk:4.5 -target:winexe -optimize+ \
  -r:System.dll \
  -r:System.Core.dll \
  -r:System.Drawing.dll \
  -r:System.Windows.Forms.dll \
  -resource:assets/skel.gif,skel.gif \
  -out:Skull.exe \
  src/Program.cs

echo "[OK] Built ./Skull.exe"
