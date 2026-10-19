#!/usr/bin/env bash
# End-to-end smoke test on Linux using the hardware simulator (no Lian Li devices needed).
# Usage: tools/linux-smoke-test.sh [path to Windows "L-Connect 3" install dir, e.g. /mnt/c/Program Files/Lian-Li/L-Connect 3]
set -euo pipefail
cd "$(dirname "$0")/.."
DOTNET=${DOTNET:-$(command -v dotnet || echo ~/.dotnet/dotnet)}
export DOTNET_ROOT=${DOTNET_ROOT:-$(dirname "$(readlink -f "$DOTNET")")}
INSTALL=${1:-"/mnt/c/Program Files/Lian-Li/L-Connect 3"}
WORK=$(mktemp -d /tmp/lconnect-smoke.XXXXXX)
export LCONNECT_DATA_DIR="$WORK/data" LCONNECT_SOCKET="$WORK/lconnect.sock"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

"$DOTNET" build src/LConnect.Daemon -c Release -o "$WORK/bin" -v q -nologo >/dev/null
"$DOTNET" build src/LConnect.Cli -c Release -o "$WORK/bin" -v q -nologo >/dev/null
CTL="$WORK/bin/lconnectctl"

echo "== hidapi on this machine (real devices, no simulator)"
"$CTL" devices || true

export LCONNECT_SIMULATE=1 LCONNECT_SIMULATE_SLINF=1 LCONNECT_FAKE_GPU_TEMP=48
echo "== assets + settings import"
"$CTL" assets import --from "$INSTALL" | tail -3
"$CTL" import --from "$INSTALL/appdata" | tail -2

echo "== daemon"
"$WORK/bin/lconnectd" >"$WORK/daemon.log" 2>&1 &
DPID=$!
trap 'kill $DPID 2>/dev/null || true; wait $DPID 2>/dev/null || true' EXIT
for _ in $(seq 1 40); do [ -S "$LCONNECT_SOCKET" ] && break; sleep 0.25; done
sleep 3
"$CTL" status
ls -l "$LCONNECT_SOCKET"

echo "== LCD animation via ffmpeg"
ANIM=$("$CTL" lcd animations | sed -n 2p | tr -d ' ')
"$CTL" lcd show --port 2 --animation "$ANIM"
sleep 4
echo "frames received by simulated LCD: $(cat "$LCONNECT_DATA_DIR/sim/tl-lcd-p2-i0-frames.txt")"

echo "== lighting + fan"
"$CTL" tl lighting --port 2 --mode Rainbow --speed 75 --brightness 100
"$CTL" tl fan --port 2 --mode Quiet --source GPU
sleep 2
tail -3 "$LCONNECT_DATA_DIR/sim/tl-controller.log"

echo "== daemon log"
cat "$WORK/daemon.log"
echo "OK (work dir: $WORK)"
