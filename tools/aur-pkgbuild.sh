#!/usr/bin/env bash
# Writes an AUR package for a release into <out dir>: a PKGBUILD generated from packaging/arch/PKGBUILD, plus the
# install script. By default it builds the release's source tarball (lconnect-linux); with --bin it repackages the
# release's prebuilt binary tarball (lconnect-linux-bin, no build step). Both share package().
# Usage: tools/aur-pkgbuild.sh [--bin] <version, e.g. 2.0.11.1> <sha256 of the tarball> <out dir>
set -euo pipefail
cd "$(dirname "$0")/.."
bin=0
if [[ ${1:-} == --bin ]]; then
	bin=1
	shift
fi
version=$1 sha256=$2 out=$3
[[ $version =~ ^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "version must look like 2.0.11.1, got '$version'" >&2; exit 1; }
[[ $sha256 =~ ^[0-9a-f]{64}$ ]] || { echo "not a sha256: '$sha256'" >&2; exit 1; }
mkdir -p "$out"

# Drops the local-build header comment and pkgver() (with its comment), pins pkgver and the checksum, points _src
# and source at the tarball and removes the git makedepend. --bin also renames the package, drops build(), check()
# and the makedepends, and takes package()'s binaries and system files from the binary tarball.
awk -v version="$version" -v sha256="$sha256" -v bin="$bin" '
	BEGIN { print "# Maintainer: Koen Hendriks <info@koenhendriks.nl>"; print ""; header = 1 }
	header && !/^pkgname=/ { next }
	{ header = 0 }
	/^# Releases are tagged/ || (bin && (/^build\(\) \{/ || /^check\(\) \{/)) { skip = 1 }
	skip { if (/^}/) { skip = 0; dropblank = 1 } next }
	dropblank && /^$/ { dropblank = 0; next }
	{ dropblank = 0 }
	/^pkgname=/ && bin { print "pkgname=lconnect-linux-bin"; next }
	/^pkgver=/ { print "pkgver=" version; next }
	/^pkgdesc=/ && bin { sub(/"$/, " (prebuilt binaries)\"") }
	/^license=/ && bin { print; print "provides=(\047lconnect-linux\047)"; print "conflicts=(\047lconnect-linux\047)"; next }
	/^makedepends=/ { if (bin) next; sub(/ \047git\047/, "") }
	/^_src=/ { print (bin ? "_src=\"lconnect-linux-$pkgver-linux-x64\"" : "_src=\"$pkgname-$pkgver\""); next }
	/^source=/ {
		if (bin) print "source=(\"$_src.tar.gz::$url/releases/download/v$pkgver/$_src.tar.gz\")"
		else print "source=(\"$url/releases/download/v$pkgver/$pkgname-$pkgver.tar.gz\")"
		next
	}
	/^sha256sums=/ { print "sha256sums=(\047" sha256 "\047)"; next }
	/^\tlocal lib=/ && bin { print "\tlocal lib=\"$srcdir/$_src/lib\" sys=\"$srcdir/$_src/system\""; next }
	{ print }
' packaging/arch/PKGBUILD >"$out/PKGBUILD"
cp packaging/arch/lconnect.install "$out/"

check() { grep -q "$1" "$out/PKGBUILD" && { echo "$2 survived the conversion" >&2; exit 1; }; return 0; }
check '^pkgver()' 'pkgver()'
check 'git+file' 'the local source'
if ((bin)); then
	check '^build()' 'build()'
	check 'srcdir/publish' 'the build output path'
	grep -q '^provides=' "$out/PKGBUILD" || { echo "provides= missing" >&2; exit 1; }
fi
exit 0
