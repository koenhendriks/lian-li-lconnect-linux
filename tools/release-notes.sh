#!/usr/bin/env bash
# Prints the release notes for a tag: every commit since the previous v* tag, newest first.
# Usage: tools/release-notes.sh <tag, e.g. v2.0.11.1>   (needs the full history and tags, e.g. fetch-depth: 0)
set -euo pipefail
tag=$1
repo=https://github.com/koenhendriks/lian-li-lconnect-linux
previous=$(git describe --tags --abbrev=0 --match 'v*' "$tag^" 2>/dev/null || true)

echo "## Changes"
echo
if [[ -n $previous ]]; then
	git log --no-merges --format="- %s ([%h]($repo/commit/%H))" "$previous..$tag"
	echo
	echo "**Full changelog:** [$previous...$tag]($repo/compare/$previous...$tag)"
else
	echo "First release."
	echo
	git log --no-merges --format="- %s ([%h]($repo/commit/%H))" "$tag"
fi
cat <<EOF

## Install

- **Arch Linux (AUR):** \`yay -S lconnect-linux\` (or any AUR helper), or build \`packaging/arch\` from a clone.
- **Other distributions:** \`lconnect-linux-${tag#v}-linux-x64.tar.gz\` holds the binaries (needs the .NET 10 and
  ASP.NET Core 10 runtimes, hidapi and ffmpeg) and the systemd unit, udev rules and desktop files in \`system/\`.

See the [README]($repo#readme) for setup (group membership, importing the LCD assets from L-Connect 3).
EOF
