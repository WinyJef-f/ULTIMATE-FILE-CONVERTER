#!/bin/bash
# Embeds every conversion tool and its dylib dependencies inside the built .app.
# Usage: bundle-tools.sh path/to/ULTIMATE-FILE-CONVERTER.app
#
# After bundling, the .app is fully self-contained:
#   Contents/Resources/bin/        - tool binaries (ffmpeg, magick, pandoc, gs, rsvg-convert)
#   Contents/Resources/lib/        - dylib dependencies (rewritten to @executable_path/../lib)
#   Contents/Resources/LibreOffice.app/ - LibreOffice for Office conversions
#
# Tool.swift in the app already looks for tools under Contents/Resources/bin first.
set -euo pipefail

if [[ $# -ne 1 ]]; then
    echo "Usage: $0 path/to/ULTIMATE-FILE-CONVERTER.app"
    exit 1
fi
APP="$1"
if [[ ! -d "$APP" ]]; then
    echo "ERROR: $APP not found"
    exit 1
fi

BREW=/opt/homebrew
BIN_DST="$APP/Contents/Resources/bin"
LIB_DST="$APP/Contents/Resources/lib"
mkdir -p "$BIN_DST" "$LIB_DST"

TOOLS=(ffmpeg pandoc 7z)

for tool in "${TOOLS[@]}"; do
    src="$BREW/bin/$tool"
    if [[ ! -x "$src" ]]; then
        echo "ERROR: $src not found. Install via brew first."
        exit 1
    fi
    echo "==> Bundling $tool..."
    cp "$src" "$BIN_DST/$tool"
    chmod +x "$BIN_DST/$tool"

    # Walk dylib deps recursively into Resources/lib/, rewriting install names.
    # -of (overwrite files)  -b (bundle deps)  -p (install_name prefix)  -s (extra search path)
    "$BREW/bin/dylibbundler" \
        -of \
        -b \
        -x "$BIN_DST/$tool" \
        -d "$LIB_DST/" \
        -p "@executable_path/../lib/" \
        -s "$BREW/lib" \
        2>&1 | tail -3 || {
            echo "WARN: dylibbundler reported issues for $tool — continuing."
        }

    # dylibbundler can leave behind one or more redundant LC_RPATH entries.
    # We rewrote install_names to literal @executable_path/../lib/... so no
    # rpath is needed. Strip every copy.
    while install_name_tool -delete_rpath "@executable_path/../lib/" "$BIN_DST/$tool" >/dev/null 2>&1; do
        :
    done
done

# Bundle LibreOffice (heavy: ~700 MB)
if [[ -d /Applications/LibreOffice.app ]]; then
    echo "==> Bundling LibreOffice.app (~700 MB)..."
    rm -rf "$APP/Contents/Resources/LibreOffice.app"
    cp -R /Applications/LibreOffice.app "$APP/Contents/Resources/LibreOffice.app"
else
    echo "WARN: /Applications/LibreOffice.app not found; Office formats will not work."
fi

# Re-sign the .app ad-hoc so the rewritten binaries don't trip Gatekeeper.
echo "==> Re-signing .app ad-hoc..."
codesign --force --deep --sign - "$APP" 2>&1 | tail -3 || true

RES_SIZE=$(du -sh "$APP/Contents/Resources" | cut -f1)
echo "==> Done. Bundled Resources size: $RES_SIZE"
