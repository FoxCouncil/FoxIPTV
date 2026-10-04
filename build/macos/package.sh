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

NOTARIZE=false

if [ -n "${MACOS_CERTIFICATE:-}" ]; then
    KEYCHAIN="$WORK/signing.keychain-db"
    KEYCHAIN_PASSWORD="$(uuidgen)"

    echo "$MACOS_CERTIFICATE" | base64 --decode > "$WORK/certificate.p12"
    security create-keychain -p "$KEYCHAIN_PASSWORD" "$KEYCHAIN"
    security set-keychain-settings -lut 3600 "$KEYCHAIN"
    security unlock-keychain -p "$KEYCHAIN_PASSWORD" "$KEYCHAIN"
    security import "$WORK/certificate.p12" -k "$KEYCHAIN" -P "$MACOS_CERTIFICATE_PASSWORD" -T /usr/bin/codesign
    security set-key-partition-list -S apple-tool:,apple:,codesign: -s -k "$KEYCHAIN_PASSWORD" "$KEYCHAIN"
    security list-keychains -d user -s "$KEYCHAIN" $(security list-keychains -d user | tr -d '"')

    IDENTITY="$(security find-identity -v -p codesigning "$KEYCHAIN" | grep -o '"Developer ID Application: [^"]*"' | head -1 | tr -d '"')"

    if [ -z "$IDENTITY" ]; then
        echo "No Developer ID Application identity in MACOS_CERTIFICATE" >&2
        exit 1
    fi

    codesign --force --timestamp --options runtime --entitlements "$HERE/FoxIPTV.entitlements" --sign "$IDENTITY" "$APP"

    if [ -n "${APPLE_ID:-}" ] && [ -n "${APPLE_TEAM_ID:-}" ] && [ -n "${APPLE_APP_PASSWORD:-}" ]; then
        NOTARIZE=true
    fi
else
    echo "MACOS_CERTIFICATE is not set, signing ad-hoc; macOS will ask users to approve the first launch"
    codesign --force --options runtime --entitlements "$HERE/FoxIPTV.entitlements" --sign - "$APP"
fi

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

if [ -n "${IDENTITY:-}" ]; then
    codesign --force --timestamp --sign "$IDENTITY" "$ROOT/$NAME.dmg"
fi

if [ "$NOTARIZE" = true ]; then
    xcrun notarytool submit "$ROOT/$NAME.dmg" --apple-id "$APPLE_ID" --team-id "$APPLE_TEAM_ID" --password "$APPLE_APP_PASSWORD" --wait
    xcrun stapler staple "$ROOT/$NAME.dmg"
    xcrun stapler staple "$APP"
fi

ditto -c -k --sequesterRsrc --keepParent "$APP" "$ROOT/$NAME.zip"

ls -la "$ROOT/$NAME.dmg" "$ROOT/$NAME.zip"
