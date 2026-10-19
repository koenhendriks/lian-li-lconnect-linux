#!/usr/bin/env bash
# Installs the daemon + CLI with the real packaging files (unit, sysusers, tmpfiles, udev, sleep hook) on a
# systemd machine and runs lconnectd as a hardened service with the hardware simulator. Needs root (sudo).
# Usage: tools/systemd-integration-test.sh <Windows L-Connect 3 install dir> [--keep]
set -euo pipefail
cd "$(dirname "$0")/.."
INSTALL_SRC=${1:?"path to the Windows 'L-Connect 3' install dir"}
KEEP=${2:-}
DOTNET=${DOTNET:-$(command -v dotnet || echo ~/.dotnet/dotnet)}
DOTNET_DIR=$(dirname "$(readlink -f "$DOTNET")")
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
PUB=$(mktemp -d /tmp/lconnect-pub.XXXXXX)
for p in LConnect.Daemon LConnect.Cli; do
	"$DOTNET" publish "src/$p" -c Release -r linux-x64 --self-contained false -p:DebugType=None -o "$PUB" -v q -nologo >/dev/null
done

P=packaging/arch
sudo install -dm755 /usr/lib/lconnect
sudo cp -a "$PUB/." /usr/lib/lconnect/ && sudo chmod 755 /usr/lib/lconnect
sudo ln -sf /usr/lib/lconnect/lconnectctl /usr/bin/lconnectctl
sudo install -Dm644 $P/lconnectd.service /usr/lib/systemd/system/lconnectd.service
sudo install -Dm644 $P/lconnect.sysusers /usr/lib/sysusers.d/lconnect.conf
sudo install -Dm644 $P/lconnect.tmpfiles /usr/lib/tmpfiles.d/lconnect.conf
sudo install -Dm644 $P/60-lconnect.rules /usr/lib/udev/rules.d/60-lconnect.rules
sudo install -Dm755 $P/lconnect.sleep /usr/lib/systemd/system-sleep/lconnect
# Test-only drop-in: simulator, and a readable copy of a user-local .NET runtime when no system one is installed
# (the service user cannot read /home/<you>/.dotnet).
SVC_DOTNET=/usr/share/dotnet
if [ ! -x /usr/share/dotnet/dotnet ]; then
	SVC_DOTNET=/opt/lconnect-test-dotnet
	sudo rm -rf /opt/lconnect-test-dotnet
	sudo cp -a "$DOTNET_DIR" /opt/lconnect-test-dotnet
	sudo chmod -R a+rX /opt/lconnect-test-dotnet
fi
sudo install -dm755 /etc/systemd/system/lconnectd.service.d
printf '[Service]\nEnvironment=LCONNECT_SIMULATE=1\nEnvironment=LCONNECT_SIMULATE_SLINF=1\nEnvironment=DOTNET_ROOT=%s\n' \
	"$SVC_DOTNET" | sudo tee /etc/systemd/system/lconnectd.service.d/test.conf >/dev/null

sudo systemd-sysusers lconnect.conf
sudo systemd-tmpfiles --create lconnect.conf
sudo udevadm control --reload || true
sudo usermod -aG lconnect "$USER"
systemd-analyze verify /usr/lib/systemd/system/lconnectd.service && echo "unit verifies"

# Run a command as the current user with the 'lconnect' group added, like after re-login.
asmember() {
	sudo setpriv --reuid="$(id -u)" --regid="$(id -g)" --groups="$(id -G | tr ' ' ','),$(getent group lconnect | cut -d: -f3)" 		env HOME="$HOME" DOTNET_ROOT="$DOTNET_DIR" "$@"
}

# Import as a group member, exactly like a user would (asmember adds the new group without re-login).
asmember LCONNECT_SIMULATE=1 LCONNECT_SIMULATE_SLINF=1 lconnectctl assets import --from "$INSTALL_SRC" | tail -2
asmember LCONNECT_SIMULATE=1 LCONNECT_SIMULATE_SLINF=1 lconnectctl import --from "$INSTALL_SRC/appdata" | tail -2

sudo systemctl daemon-reload
sudo systemctl restart lconnectd
sleep 6
systemctl --no-pager status lconnectd | head -12
ls -ld /var/lib/lconnect /run/lconnect /run/lconnect/lconnect.sock
sudo find /var/lib/lconnect -maxdepth 2 \( ! -group lconnect -o ! -perm -g+w \) -printf 'not group-writable: %p\n' | head
asmember lconnectctl status
asmember lconnectctl tl lighting --port 2 --mode Rainbow --speed 50
sudo env DOTNET_ROOT=$SVC_DOTNET /usr/lib/systemd/system-sleep/lconnect post && echo "sleep hook ran"
sleep 2
sudo tail -3 /var/lib/lconnect/sim/tl-controller.log
journalctl -u lconnectd --no-pager -n 25

if [ "$KEEP" != "--keep" ]; then
	sudo systemctl stop lconnectd
	sudo rm -rf /etc/systemd/system/lconnectd.service.d /usr/lib/systemd/system/lconnectd.service /usr/lib/sysusers.d/lconnect.conf \
		/usr/lib/tmpfiles.d/lconnect.conf /usr/lib/udev/rules.d/60-lconnect.rules /usr/lib/systemd/system-sleep/lconnect \
		/usr/lib/lconnect /usr/bin/lconnectctl /var/lib/lconnect /var/cache/lconnect /opt/lconnect-test-dotnet
	sudo systemctl daemon-reload
	sudo gpasswd -d "$USER" lconnect >/dev/null || true
	sudo userdel lconnect 2>/dev/null || true
	sudo groupdel lconnect 2>/dev/null || true
	echo "cleaned up"
fi
