#!/bin/bash
# Regenerates the app icon from tools/logo.svg into the Asset Catalog.
# xcodebuild consumes Resources/Assets.xcassets/AppIcon.appiconset/ to set
# CFBundleIconName in the built Info.plist and to embed Assets.car in the bundle.
set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
LOGO_SVG="$PROJECT_DIR/tools/logo.svg"
APPICONSET_DIR="$PROJECT_DIR/Resources/Assets.xcassets/AppIcon.appiconset"

if [[ ! -f "$LOGO_SVG" ]]; then
    echo "ERROR: $LOGO_SVG not found"
    exit 1
fi
if [[ ! -d "$APPICONSET_DIR" ]]; then
    echo "ERROR: $APPICONSET_DIR not found (Asset Catalog missing Contents.json)"
    exit 1
fi

SCRATCH=$(mktemp -d)
trap "rm -rf '$SCRATCH'" EXIT

echo "==> Rendering 1024x1024 master PNG from SVG..."
/opt/homebrew/bin/rsvg-convert -w 1024 -h 1024 "$LOGO_SVG" -o "$SCRATCH/icon-1024.png"

echo "==> Generating all iconset sizes..."
rm -f "$APPICONSET_DIR"/icon_*.png
for s in 16 32 128 256 512; do
    d=$((s * 2))
    /opt/homebrew/bin/magick "$SCRATCH/icon-1024.png" -resize ${s}x${s}  "$APPICONSET_DIR/icon_${s}x${s}.png"
    /opt/homebrew/bin/magick "$SCRATCH/icon-1024.png" -resize ${d}x${d}  "$APPICONSET_DIR/icon_${s}x${s}@2x.png"
done

echo "==> Done. Asset Catalog has $(ls "$APPICONSET_DIR"/icon_*.png | wc -l | tr -d ' ') PNGs."
