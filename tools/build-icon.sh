#!/bin/bash
# Regenerates Resources/AppIcon.icns from tools/logo.svg.
set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
LOGO_SVG="$PROJECT_DIR/tools/logo.svg"
RESOURCES_DIR="$PROJECT_DIR/Resources"

if [[ ! -f "$LOGO_SVG" ]]; then
    echo "ERROR: $LOGO_SVG not found"
    exit 1
fi

SCRATCH=$(mktemp -d)
trap "rm -rf '$SCRATCH'" EXIT

ICONSET="$SCRATCH/AppIcon.iconset"
mkdir -p "$ICONSET"

echo "==> Rendering 1024x1024 master PNG from SVG..."
/opt/homebrew/bin/rsvg-convert -w 1024 -h 1024 "$LOGO_SVG" -o "$SCRATCH/icon-1024.png"

echo "==> Generating iconset sizes..."
for s in 16 32 128 256 512; do
    d=$((s * 2))
    /opt/homebrew/bin/magick "$SCRATCH/icon-1024.png" -resize ${s}x${s}  "$ICONSET/icon_${s}x${s}.png"
    /opt/homebrew/bin/magick "$SCRATCH/icon-1024.png" -resize ${d}x${d}  "$ICONSET/icon_${s}x${s}@2x.png"
done

echo "==> Compiling .icns..."
mkdir -p "$RESOURCES_DIR"
iconutil -c icns -o "$RESOURCES_DIR/AppIcon.icns" "$ICONSET"
echo "    Wrote $RESOURCES_DIR/AppIcon.icns ($(du -h "$RESOURCES_DIR/AppIcon.icns" | cut -f1))"
