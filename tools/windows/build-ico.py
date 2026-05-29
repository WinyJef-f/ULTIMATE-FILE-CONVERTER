#!/usr/bin/env python3
"""Pack existing PNG icon renders into a Windows .ico (PNG-compressed entries).

Windows 10/11 fully support PNG-compressed ICO directory entries, so we can embed
the already-rendered PNGs from the macOS asset catalog directly — no image library
needed. Run from the repo root:

    python3 tools/windows/build-ico.py
"""
import struct, sys, os

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(REPO, "Resources", "Assets.xcassets", "AppIcon.appiconset")
OUT = os.path.join(REPO, "Windows", "UltimateFileConverter.WinUI", "Assets", "app.ico")

# (logical size, source png) — 256 is encoded as 0 in the directory entry.
ENTRIES = [
    (16,  "icon_16x16.png"),
    (32,  "icon_32x32.png"),
    (64,  "icon_32x32@2x.png"),
    (128, "icon_128x128.png"),
    (256, "icon_256x256.png"),
]

def png_dims(data: bytes):
    # IHDR width/height are big-endian uint32 at offset 16 and 20.
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError("not a PNG")
    w, h = struct.unpack(">II", data[16:24])
    return w, h

def main():
    images = []
    for size, name in ENTRIES:
        path = os.path.join(SRC, name)
        with open(path, "rb") as f:
            data = f.read()
        w, h = png_dims(data)
        if (w, h) != (size, size):
            print(f"warning: {name} is {w}x{h}, expected {size}x{size}", file=sys.stderr)
        images.append((size, data))

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    count = len(images)
    header = struct.pack("<HHH", 0, 1, count)  # reserved, type=1 (icon), count
    offset = 6 + 16 * count
    entries = b""
    payload = b""
    for size, data in images:
        b = 0 if size >= 256 else size
        entries += struct.pack(
            "<BBBBHHII",
            b, b,            # width, height (0 == 256)
            0, 0,            # color count, reserved
            1, 32,           # color planes, bits-per-pixel
            len(data), offset,
        )
        payload += data
        offset += len(data)

    with open(OUT, "wb") as f:
        f.write(header + entries + payload)
    print(f"wrote {OUT} ({count} sizes, {os.path.getsize(OUT)} bytes)")

if __name__ == "__main__":
    main()
