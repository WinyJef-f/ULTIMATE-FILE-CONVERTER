#!/bin/bash
# Builds a signed-with-ad-hoc .pkg installer (and matching .dmg) for ULTIMATE-FILE-CONVERTER.
# Output: dist/ULTIMATE-FILE-CONVERTER-Installer-<version>.pkg
#         dist/ULTIMATE-FILE-CONVERTER-<version>.dmg
set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$PROJECT_DIR"

VERSION="1.0.2"
BUNDLE_ID="com.jeffreyheiler.UltimateFileConverter"
APP_NAME="ULTIMATE-FILE-CONVERTER"

DIST_DIR="$PROJECT_DIR/dist"
SCRATCH=$(mktemp -d)
# Derived data lives inside the scratch dir so each build starts clean —
# avoids ad-hoc-signed stale binaries blocking the next build.
BUILD_DIR="$SCRATCH/build-release"
trap "rm -rf '$SCRATCH' 2>/dev/null || true" EXIT

mkdir -p "$DIST_DIR"

echo "==> Generating Xcode project..."
/opt/homebrew/bin/xcodegen generate >/dev/null

echo "==> Building Release .app..."
DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
    /usr/bin/xcodebuild \
        -project "$APP_NAME.xcodeproj" \
        -scheme "$APP_NAME" \
        -configuration Release \
        -derivedDataPath "$BUILD_DIR" \
        -destination 'platform=macOS' \
        build > "$SCRATCH/xcodebuild.log" 2>&1 || {
    echo "Release build FAILED. Tail of log:"
    tail -40 "$SCRATCH/xcodebuild.log"
    exit 1
}

RELEASE_APP="$BUILD_DIR/Build/Products/Release/$APP_NAME.app"
if [[ ! -d "$RELEASE_APP" ]]; then
    echo "ERROR: Release app not found at $RELEASE_APP"
    exit 1
fi
echo "    .app at $RELEASE_APP ($(du -sh "$RELEASE_APP" | cut -f1))"

echo "==> Staging payload (tools installed on first launch via Homebrew — not bundled)..."
PAYLOAD_ROOT="$SCRATCH/payload"
mkdir -p "$PAYLOAD_ROOT/Applications"
cp -R "$RELEASE_APP" "$PAYLOAD_ROOT/Applications/"

echo "==> Building component pkg (no postinstall — everything is bundled)..."
COMPONENT_PKG="$SCRATCH/component.pkg"
/usr/bin/pkgbuild \
    --root "$PAYLOAD_ROOT" \
    --identifier "$BUNDLE_ID" \
    --version "$VERSION" \
    --install-location "/" \
    "$COMPONENT_PKG"

echo "==> Building distribution pkg..."
FINAL_PKG="$DIST_DIR/$APP_NAME-Installer-$VERSION.pkg"
rm -f "$FINAL_PKG"
(
    cd "$SCRATCH"
    /usr/bin/productbuild \
        --distribution "$PROJECT_DIR/tools/installer/Distribution.xml" \
        --resources "$PROJECT_DIR/tools/installer/resources" \
        --package-path "$SCRATCH" \
        "$FINAL_PKG"
)
echo "    Installer pkg at $FINAL_PKG ($(du -h "$FINAL_PKG" | cut -f1))"

echo "==> Wrapping in DMG..."
DMG_STAGING="$SCRATCH/dmg-staging"
mkdir -p "$DMG_STAGING"
cp "$FINAL_PKG" "$DMG_STAGING/"

DMG_PATH="$DIST_DIR/$APP_NAME-$VERSION.dmg"
rm -f "$DMG_PATH"
/usr/bin/hdiutil create \
    -volname "$APP_NAME" \
    -srcfolder "$DMG_STAGING" \
    -ov \
    -format UDZO \
    "$DMG_PATH" >/dev/null

echo "    DMG at $DMG_PATH ($(du -h "$DMG_PATH" | cut -f1))"

echo ""
echo "==> Done. dist/ contents:"
ls -lh "$DIST_DIR/"
