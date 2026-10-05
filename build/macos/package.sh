#!/usr/bin/env bash
# Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

set -euo pipefail

VERSION="${1:?usage: build/macos/package.sh <version> <publish dir> <output name>}"
PUBLISH="${2:?usage: build/macos/package.sh <version> <publish dir> <output name>}"
NAME="${3:?usage: build/macos/package.sh <version> <publish dir> <output name>}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
HERE="$ROOT/build/macos"
WORK="$(mktemp -d)"
APP="$WORK/FoxIPTV.app"
SHORT_VERSION="${VERSION%%-*}"

mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$PUBLISH/FoxIPTV" "$APP/Contents/MacOS/FoxIPTV"
chmod 755 "$APP/Contents/MacOS/FoxIPTV"
cp "$ROOT/src/FoxIPTV/Assets/FoxIPTV.icns" "$APP/Contents/Resources/FoxIPTV.icns"
cp "$ROOT/src/FoxIPTV/Info.plist" "$APP/Contents/Info.plist"
/usr/libexec/PlistBuddy -c "Set :CFBundleVersion $VERSION" -c "Set :CFBundleShortVersionString $SHORT_VERSION" "$APP/Contents/Info.plist"

codesign --force --options runtime --entitlements "$HERE/FoxIPTV.entitlements" --sign - "$APP"

codesign --verify --strict --verbose=2 "$APP"

tiffutil -cathidpicheck "$HERE/dmg-background.png" "$HERE/dmg-background@2x.png" -out "$WORK/background.tiff"

mkdir "$WORK/dmg"
cp -R "$APP" "$WORK/dmg/"

create-dmg \
    --volname "FoxIPTV" \
    --volicon "$APP/Contents/Resources/FoxIPTV.icns" \
    --background "$WORK/background.tiff" \
    --window-pos 200 120 \
    --window-size 660 400 \
    --icon-size 128 \
    --icon "FoxIPTV.app" 180 180 \
    --hide-extension "FoxIPTV.app" \
    --app-drop-link 480 180 \
    "$ROOT/$NAME.dmg" \
    "$WORK/dmg"

ditto -c -k --sequesterRsrc --keepParent "$APP" "$ROOT/$NAME.zip"

ls -la "$ROOT/$NAME.dmg" "$ROOT/$NAME.zip"
