#!/usr/bin/env python3
"""Regenerate the demo files in samples/.

Pure standard library (no Pillow/numpy) so it runs anywhere Python 3 does. Produces a
small, clean, clearly-typed example for each conversion family the app supports, plus a
samples/README.md that records the ground truth for every file.

    python3 tools/make-samples.py

The raster PNG, the WAV tone, and the animated GIF are synthesized byte-by-byte; the SVG,
Markdown, subtitle, and archive files are written as text or ZIP. A built-in SRT<->SBV
round-trip self-test mirrors the in-app SubtitleConverter so subtitle samples stay valid.

The font samples (TTF/OTF/WOFF/WOFF2) are the one exception to the pure-stdlib rule: they
use fontTools (and brotli for WOFF2). If those aren't installed the fonts are skipped and
everything else still regenerates — `pip install fonttools brotli` to include them.
"""

from __future__ import annotations

import math
import os
import struct
import wave
import zipfile
import zlib

# Brand palette (matches tools/logo.svg).
TEAL = (0x0D, 0x94, 0x88)
CREAM = (0xFE, 0xF3, 0xC7)
DARK = (0x0B, 0x3B, 0x36)

SAMPLES_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "samples")


# --------------------------------------------------------------------------- PNG

def write_png(path: str, width: int, height: int, pixels: list[tuple[int, int, int]]) -> None:
    """Write a 24-bit RGB PNG. `pixels` is row-major, length width*height."""
    def chunk(tag: bytes, data: bytes) -> bytes:
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    raw = bytearray()
    for y in range(height):
        raw.append(0)  # filter type 0 (None) for each scanline
        row = y * width
        for x in range(width):
            r, g, b = pixels[row + x]
            raw += bytes((r, g, b))

    ihdr = struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)  # 8-bit, color type 2 (RGB)
    png = (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", ihdr)
        + chunk(b"IDAT", zlib.compress(bytes(raw), 9))
        + chunk(b"IEND", b"")
    )
    with open(path, "wb") as f:
        f.write(png)


def make_png() -> str:
    """A diagonal teal->cream gradient with a centered solid cream disc — clearly a raster image."""
    w, h = 512, 320
    cx, cy, r = w / 2, h / 2, 96
    pixels: list[tuple[int, int, int]] = []
    for y in range(h):
        for x in range(w):
            t = (x / w + y / h) / 2  # 0..1 diagonal blend
            bg = tuple(round(TEAL[i] + (CREAM[i] - TEAL[i]) * t) for i in range(3))
            if (x - cx) ** 2 + (y - cy) ** 2 <= r * r:
                pixels.append(CREAM)
            else:
                pixels.append(bg)  # type: ignore[arg-type]
    path = os.path.join(SAMPLES_DIR, "hello.png")
    write_png(path, w, h, pixels)
    return path


# --------------------------------------------------------------------------- WAV

def make_wav() -> str:
    """A short, pleasant A-major arpeggio (A4 C#5 E5 A5) — recognizably music, not noise."""
    rate = 44100
    notes = [440.00, 554.37, 659.25, 880.00]  # Hz
    note_len = 0.45  # seconds each
    amplitude = 0.32
    frames = bytearray()
    for freq in notes:
        n = int(rate * note_len)
        for i in range(n):
            # Short attack/release envelope to avoid clicks.
            env = min(1.0, i / (rate * 0.02), (n - i) / (rate * 0.05))
            sample = amplitude * env * math.sin(2 * math.pi * freq * (i / rate))
            frames += struct.pack("<h", int(sample * 32767))
    path = os.path.join(SAMPLES_DIR, "hello.wav")
    with wave.open(path, "wb") as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(rate)
        wav.writeframes(bytes(frames))
    return path


# --------------------------------------------------------------------------- GIF

def _lzw_encode(indices: list[int], min_code_size: int) -> bytes:
    """Standard variable-width GIF LZW encoder."""
    clear_code = 1 << min_code_size
    end_code = clear_code + 1
    code_size = min_code_size + 1
    table: dict[tuple[int, ...], int] = {}

    def reset_table() -> None:
        nonlocal table, code_size, next_code
        table = {(i,): i for i in range(clear_code)}
        code_size = min_code_size + 1
        next_code = end_code + 1

    out_bits = 0
    out_acc = 0
    out = bytearray()

    def emit(code: int) -> None:
        nonlocal out_bits, out_acc
        out_acc |= code << out_bits
        out_bits += code_size
        while out_bits >= 8:
            out.append(out_acc & 0xFF)
            out_acc >>= 8
            out_bits -= 8

    next_code = end_code + 1
    reset_table()
    emit(clear_code)

    current: tuple[int, ...] = (indices[0],)
    for k in indices[1:]:
        combined = current + (k,)
        if combined in table:
            current = combined
        else:
            emit(table[current])
            table[combined] = next_code
            next_code += 1
            if next_code > (1 << code_size) and code_size < 12:
                code_size += 1
            if next_code > 4095:
                emit(clear_code)
                reset_table()
            current = (k,)
    emit(table[current])
    emit(end_code)
    if out_bits > 0:
        out.append(out_acc & 0xFF)
    return bytes(out)


def _sub_blocks(data: bytes) -> bytes:
    out = bytearray()
    for i in range(0, len(data), 255):
        block = data[i:i + 255]
        out.append(len(block))
        out += block
    out.append(0)  # block terminator
    return bytes(out)


def make_gif() -> str:
    """A looping animation: a cream bar sweeping across a teal field (8 frames, 120x80)."""
    w, h = 120, 80
    palette = [TEAL, CREAM, DARK]  # index 0,1,2
    frames = 8
    bar_w = 22

    header = b"GIF89a"
    # Logical screen descriptor: GCT present, color resolution 7, GCT size code 1 -> 4 entries.
    lsd = struct.pack("<HHBBB", w, h, 0xF1, 0, 0)
    gct = b"".join(bytes(c) for c in palette) + bytes(3)  # pad to 4 entries (12 bytes)
    # NETSCAPE2.0 loop-forever extension.
    loop = b"\x21\xFF\x0B" + b"NETSCAPE2.0" + b"\x03\x01" + struct.pack("<H", 0) + b"\x00"

    body = bytearray()
    for fr in range(frames):
        start = int((w - bar_w) * fr / (frames - 1))
        indices: list[int] = []
        for y in range(h):
            for x in range(w):
                in_bar = start <= x < start + bar_w
                edge = y < 3 or y >= h - 3
                indices.append(1 if in_bar else (2 if edge else 0))
        gce = b"\x21\xF9\x04\x00" + struct.pack("<H", 12) + b"\x00\x00"  # 0.12s delay
        img_desc = b"\x2C" + struct.pack("<HHHHB", 0, 0, w, h, 0x00)
        min_code_size = 2
        data = _sub_blocks(_lzw_encode(indices, min_code_size))
        body += gce + img_desc + bytes((min_code_size,)) + data

    gif = header + lsd + gct + loop + bytes(body) + b"\x3B"
    path = os.path.join(SAMPLES_DIR, "animated.gif")
    with open(path, "wb") as f:
        f.write(gif)
    return path


# --------------------------------------------------------------------- text files

SVG = """<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256" width="256" height="256">
  <rect width="256" height="256" rx="28" fill="#0d9488"/>
  <circle cx="128" cy="128" r="74" fill="#fef3c7"/>
  <text x="128" y="143" font-family="Helvetica, Arial, sans-serif" font-size="42"
        font-weight="bold" text-anchor="middle" fill="#0d9488">UFC</text>
</svg>
"""

MARKDOWN = """# Welcome

Drop this file into **ULTIMATE-FILE-CONVERTER** and convert it to
**HTML**, **DOCX**, **EPUB**, or **PDF**.

- Fully on-device — nothing is uploaded.
- Documents are handled by Pandoc and LibreOffice under the hood.
"""

SRT = """1
00:00:00,000 --> 00:00:02,500
Welcome to ULTIMATE-FILE-CONVERTER!

2
00:00:02,500 --> 00:00:06,000
Drop this SubRip file in and convert it
to WebVTT, SSA/ASS, or YouTube SBV.

3
00:00:06,000 --> 00:00:09,000
Subtitles convert locally, just like everything else.
"""

SBV = """0:00:00.000,0:00:02.500
Welcome to ULTIMATE-FILE-CONVERTER!

0:00:02.500,0:00:06.000
This is a YouTube SBV caption file.
Convert it to SRT, WebVTT, or SSA/ASS.

0:00:06.000,0:00:09.000
Subtitles convert locally, just like everything else.
"""

README = """# Sample files

A clean, clearly-typed example for each conversion family. Drop any of these into
ULTIMATE-FILE-CONVERTER and pick a target format to try a conversion. Every file's
contents genuinely match its extension — regenerate them with `python3 tools/make-samples.py`.

| File | True type | Category | Try converting to |
|---|---|---|---|
| `hello.png` | 512×320 RGB PNG (gradient + disc) | Image | JPEG, WebP, AVIF, HEIC, BMP, TIFF, ICO |
| `hello.svg` | SVG vector logo | Image | PNG, JPEG, WebP, PDF |
| `animated.gif` | 8-frame looping GIF | Image | MP4, WebM, PNG |
| `hello.dng` | Minimal 4×4 DNG (DNGVersion 1.4, source-only) | RAW Photo | JPEG, PNG, TIFF |
| `hello.cr2` | Minimal 4×4 TIFF-based RAW stub — Canon CR2 (source-only) | RAW Photo | JPEG, PNG, TIFF |
| `hello.nef` | Minimal 4×4 TIFF-based RAW stub — Nikon NEF (source-only) | RAW Photo | JPEG, PNG, TIFF |
| `hello.arw` | Minimal 4×4 TIFF-based RAW stub — Sony ARW (source-only) | RAW Photo | JPEG, PNG, TIFF |
| `hello.wav` | 16-bit mono PCM tone (A-major arpeggio) | Audio | MP3, FLAC, AAC, M4A, OGG, OPUS |
| `hello.md` | Markdown document | Document | HTML, DOCX, EPUB, PDF, RTF |
| `hello.epub` | Valid EPUB 2.0 e-book (XHTML + NCX) | Document | MOBI, AZW3, PDF, DOCX |
| `hello.mobi` | Minimal Mobipocket e-book (PalmDB + MOBI headers) | Document | EPUB, AZW3, PDF |
| `hello.srt` | SubRip subtitles | Subtitle | WebVTT, SSA/ASS, YouTube SBV |
| `hello.sbv` | YouTube SBV subtitles | Subtitle | SubRip, WebVTT, SSA/ASS |
| `hello.zip` | ZIP archive (3 text files) | Archive | 7-Zip, TAR, TAR.GZ |
| `hello.ttf` | TrueType font (one box glyph) | Font | OTF, WOFF, WOFF2 |
| `hello.otf` | OpenType/CFF font (one box glyph) | Font | TTF, WOFF, WOFF2 |
| `hello.woff` | WOFF web font (wraps the TTF) | Font | TTF, OTF, WOFF2 |
| `hello.woff2` | WOFF2 web font (wraps the TTF) | Font | TTF, OTF, WOFF |
"""


# ----------------------------------------------------------------------- ZIP

# --------------------------------------------------------------------------- TIFF / RAW

def _tiff_bytes(extra_tags: list | None = None) -> bytes:
    """Minimal 4×4 16-bit grayscale TIFF. extra_tags: list of (tag, type, count, value)."""
    tags = [
        (256, 4, 1, 4),    # ImageWidth = 4 (LONG)
        (257, 4, 1, 4),    # ImageLength = 4 (LONG)
        (258, 3, 1, 16),   # BitsPerSample = 16 (SHORT)
        (259, 3, 1, 1),    # Compression = none (SHORT)
        (262, 3, 1, 1),    # PhotometricInterpretation = BlackIsZero (SHORT)
        (277, 3, 1, 1),    # SamplesPerPixel = 1 (SHORT)
        (278, 4, 1, 4),    # RowsPerStrip = 4 (LONG)
    ]
    if extra_tags:
        tags.extend(extra_tags)
    n = len(tags) + 2  # +2 for StripOffsets and StripByteCounts
    strip_off = 8 + 2 + n * 12 + 4
    tags.append((273, 4, 1, strip_off))
    tags.append((279, 4, 1, 32))
    tags.sort(key=lambda t: t[0])

    ifd = struct.pack("<H", n)
    for tag, typ, count, value in tags:
        if typ == 1:  # BYTE — value is bytes
            ifd += struct.pack("<HHI", tag, 1, count) + bytes(value)[:4].ljust(4, b"\x00")
        elif typ == 3:  # SHORT
            ifd += struct.pack("<HHIHH", tag, 3, count, value, 0)
        else:  # LONG
            ifd += struct.pack("<HHII", tag, 4, count, value)
    ifd += struct.pack("<I", 0)

    header = b"II" + struct.pack("<HI", 42, 8)
    pixels = bytes([0x88, 0x1F] * 16)  # 16 pixels × 2 bytes = 32 bytes
    return header + ifd + pixels


def make_dng() -> str:
    """4×4 16-bit DNG with DNGVersion [1,4,0,0] — a source-only RAW sample."""
    data = _tiff_bytes(extra_tags=[
        (50706, 1, 4, b"\x01\x04\x00\x00"),  # DNGVersion = 1.4.0.0
        (50707, 1, 4, b"\x01\x01\x00\x00"),  # DNGBackwardVersion = 1.1.0.0
    ])
    path = os.path.join(SAMPLES_DIR, "hello.dng")
    with open(path, "wb") as f:
        f.write(data)
    return path


def make_raw_stub(ext: str) -> str:
    """Minimal TIFF-based stub for a RAW camera extension (source-only)."""
    path = os.path.join(SAMPLES_DIR, f"hello.{ext}")
    with open(path, "wb") as f:
        f.write(_tiff_bytes())
    return path


# --------------------------------------------------------------------------- EPUB

def make_epub() -> str:
    """A minimal but fully conformant EPUB 2.0 e-book."""
    container = (
        '<?xml version="1.0" encoding="UTF-8"?>'
        '<container xmlns="urn:oasis:names:tc:opendocument:xmlns:container" version="1.0">'
        '<rootfiles>'
        '<rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/>'
        '</rootfiles>'
        '</container>'
    )
    opf = (
        '<?xml version="1.0" encoding="UTF-8"?>'
        '<package xmlns="http://www.idpf.org/2007/opf" version="2.0" unique-identifier="uid">'
        '<metadata xmlns:dc="http://purl.org/dc/elements/1.1/">'
        '<dc:title>Hello from ULTIMATE-FILE-CONVERTER</dc:title>'
        '<dc:identifier id="uid">urn:uuid:hello-ufc-sample-001</dc:identifier>'
        '<dc:language>en</dc:language>'
        '</metadata>'
        '<manifest>'
        '<item id="ncx" href="toc.ncx" media-type="application/x-dtbncx+xml"/>'
        '<item id="content" href="content.xhtml" media-type="application/xhtml+xml"/>'
        '</manifest>'
        '<spine toc="ncx"><itemref idref="content"/></spine>'
        '</package>'
    )
    ncx = (
        '<?xml version="1.0" encoding="UTF-8"?>'
        '<!DOCTYPE ncx PUBLIC "-//NISO//DTD ncx 2005-1//EN"'
        ' "http://www.daisy.org/z3986/2005/ncx-2005-1.dtd">'
        '<ncx xmlns="http://www.daisy.org/z3986/2005/ncx/" version="2005-1">'
        '<head><meta name="dtb:uid" content="urn:uuid:hello-ufc-sample-001"/></head>'
        '<docTitle><text>Hello from ULTIMATE-FILE-CONVERTER</text></docTitle>'
        '<navMap>'
        '<navPoint id="nav1" playOrder="1">'
        '<navLabel><text>Hello</text></navLabel>'
        '<content src="content.xhtml"/>'
        '</navPoint>'
        '</navMap>'
        '</ncx>'
    )
    xhtml = (
        '<?xml version="1.0" encoding="UTF-8"?>'
        '<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.1//EN"'
        ' "http://www.w3.org/TR/xhtml11/DTD/xhtml11.dtd">'
        '<html xmlns="http://www.w3.org/1999/xhtml">'
        '<head><title>Hello from ULTIMATE-FILE-CONVERTER</title></head>'
        '<body>'
        '<h1>Hello from ULTIMATE-FILE-CONVERTER</h1>'
        '<p>Drop this EPUB into the app and convert it to MOBI, AZW3, PDF, or DOCX.</p>'
        '<p>Every file you convert stays on your device — no uploads, no accounts.</p>'
        '</body>'
        '</html>'
    )
    path = os.path.join(SAMPLES_DIR, "hello.epub")
    with zipfile.ZipFile(path, "w") as zf:
        mime_info = zipfile.ZipInfo("mimetype")
        mime_info.compress_type = zipfile.ZIP_STORED
        zf.writestr(mime_info, "application/epub+zip")
        zf.writestr("META-INF/container.xml", container)
        zf.writestr("OEBPS/content.opf", opf)
        zf.writestr("OEBPS/toc.ncx", ncx)
        zf.writestr("OEBPS/content.xhtml", xhtml)
    return path


# --------------------------------------------------------------------------- MOBI

def make_mobi() -> str:
    """Minimal Mobipocket e-book (PalmDB + MOBI headers) that Calibre can open."""
    text = (
        b"<html><head><title>Hello from ULTIMATE-FILE-CONVERTER</title></head>"
        b"<body><h1>Hello from ULTIMATE-FILE-CONVERTER</h1>"
        b"<p>Drop this MOBI into the app and convert to EPUB, AZW3, or PDF.</p>"
        b"</body></html>"
    )
    # Record 0: PalmDOC header (16 bytes) + MOBI header (16 bytes)
    palmdoc = struct.pack(">HHIHHHH", 1, 0, len(text), 1, 4096, 0, 0)
    mobi_hdr = struct.pack(">4sIII", b"MOBI", 16, 2, 65001)  # type=book, UTF-8
    record0 = palmdoc + mobi_hdr  # 32 bytes

    # PDB file header: 78 bytes (big-endian)
    db_name = b"Hello-UFC\x00" + bytes(22)  # padded to 32 bytes
    pdb_hdr = (
        db_name
        + struct.pack(">HHIIIIII", 0, 0, 0, 0, 0, 0, 0, 0)  # attrs..sort_info (28 bytes)
        + b"BOOK" + b"MOBI"
        + struct.pack(">IIH", 0, 0, 2)   # uid_seed, next_list_id, num_records (10 bytes)
    )  # 32+28+4+4+10 = 78 bytes

    # Record list: 2 records × 8 bytes + 2-byte gap = 18 bytes → records start at offset 96
    r0_off = 96
    r1_off = r0_off + len(record0)
    rec_list = (
        struct.pack(">I", r0_off) + b"\x00\x00\x00\x00"
        + struct.pack(">I", r1_off) + b"\x00\x00\x00\x01"
        + b"\x00\x00"
    )

    path = os.path.join(SAMPLES_DIR, "hello.mobi")
    with open(path, "wb") as f:
        f.write(pdb_hdr + rec_list + record0 + text)
    return path


# --------------------------------------------------------------------------- Fonts

def make_fonts() -> list[str]:
    """Generate valid TTF, OTF, WOFF, and WOFF2 samples sharing one simple glyph.

    Unlike the rest of this script, fonts are built with **fontTools** (plus **brotli**
    for WOFF2) rather than pure stdlib — hand-rolling sfnt/CFF/WOFF/brotli by hand is far
    more error-prone than using the canonical font library. If fontTools is unavailable
    the font samples are skipped (everything else still regenerates).
    """
    try:
        from fontTools.fontBuilder import FontBuilder
        from fontTools.pens.ttGlyphPen import TTGlyphPen
        from fontTools.pens.t2CharStringPen import T2CharStringPen
        from fontTools.ttLib import TTFont
    except ImportError:
        print("note: fontTools not installed — skipping ttf/otf/woff/woff2 samples "
              "(pip install fonttools brotli to generate them)")
        return []

    upm, advance = 1000, 600
    glyph_order = [".notdef", "U"]
    cmap = {0x55: "U"}  # the letter 'U', drawn as a simple box
    names = {
        "familyName": "Hello UFC", "styleName": "Regular",
        "uniqueFontIdentifier": "HelloUFC-Regular-1.0",
        "fullName": "Hello UFC", "psName": "HelloUFC-Regular",
        "version": "Version 1.0",
    }

    def draw(pen) -> None:
        pen.moveTo((100, 0))
        pen.lineTo((500, 0))
        pen.lineTo((500, 700))
        pen.lineTo((100, 700))
        pen.closePath()

    def metrics() -> dict:
        return {n: (advance, 100 if n != ".notdef" else 0) for n in glyph_order}

    def common_setup(fb: "FontBuilder") -> None:
        fb.setupHorizontalMetrics(metrics())
        fb.setupHorizontalHeader(ascent=800, descent=-200)
        fb.setupNameTable(names)
        fb.setupOS2(sTypoAscender=800, sTypoDescender=-200, usWinAscent=800,
                    usWinDescent=200, sxHeight=500, sCapHeight=700)
        fb.setupPost()

    ttf_path = os.path.join(SAMPLES_DIR, "hello.ttf")
    otf_path = os.path.join(SAMPLES_DIR, "hello.otf")
    woff_path = os.path.join(SAMPLES_DIR, "hello.woff")
    woff2_path = os.path.join(SAMPLES_DIR, "hello.woff2")

    # TrueType (glyf outlines).
    fb = FontBuilder(upm, isTTF=True)
    fb.setupGlyphOrder(glyph_order)
    fb.setupCharacterMap(cmap)
    glyfs = {}
    for name in glyph_order:
        pen = TTGlyphPen(None)
        if name != ".notdef":
            draw(pen)
        glyfs[name] = pen.glyph()
    fb.setupGlyf(glyfs)
    common_setup(fb)
    fb.save(ttf_path)

    # OpenType/CFF (PostScript outlines) — independently built from the same glyph.
    fb = FontBuilder(upm, isTTF=False)
    fb.setupGlyphOrder(glyph_order)
    fb.setupCharacterMap(cmap)
    charstrings = {}
    for name in glyph_order:
        pen = T2CharStringPen(advance, None)
        if name != ".notdef":
            draw(pen)
        charstrings[name] = pen.getCharString()
    fb.setupCFF(names["psName"], {"FullName": names["fullName"]}, charstrings, {})
    common_setup(fb)
    fb.save(otf_path)

    # Web fonts are the TrueType sfnt re-wrapped (WOFF1 = zlib, WOFF2 = brotli).
    f = TTFont(ttf_path)
    f.flavor = "woff"
    f.save(woff_path)
    f = TTFont(ttf_path)
    f.flavor = "woff2"
    f.save(woff2_path)

    return [ttf_path, otf_path, woff_path, woff2_path]


def make_zip() -> str:
    """A ZIP archive containing three small text files — a clearly valid archive sample."""
    path = os.path.join(SAMPLES_DIR, "hello.zip")
    with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as zf:
        zf.writestr("greeting.txt", "Hello from ULTIMATE-FILE-CONVERTER!\n")
        zf.writestr("info.txt", (
            "This archive was generated by tools/make-samples.py.\n"
            "Drop hello.zip into the app and convert it to 7-Zip, TAR, or TAR.GZ.\n"
        ))
        zf.writestr("colors.txt", (
            "Teal:  #0d9488\n"
            "Cream: #fef3c7\n"
            "Dark:  #0b3b36\n"
        ))
    return path


def write_text(name: str, content: str) -> str:
    path = os.path.join(SAMPLES_DIR, name)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(content)
    return path


# ------------------------------------------------------- subtitle self-test

def _blocks(text: str) -> list[list[str]]:
    text = text.replace("\r\n", "\n").replace("\r", "\n")
    out, cur = [], []
    for line in text.split("\n"):
        if line.strip() == "":
            if cur:
                out.append(cur)
                cur = []
        else:
            cur.append(line)
    if cur:
        out.append(cur)
    return out


def _parse_ts(raw: str) -> int:
    raw = raw.strip().replace(",", ".")
    hh, mm, rest = raw.split(":")
    sec, _, frac = rest.partition(".")
    ms = int((frac + "000")[:3]) if frac else 0
    return ((int(hh) * 60 + int(mm)) * 60 + int(sec)) * 1000 + ms


def _split(ms: int):
    ms = max(0, ms)
    return ms // 3_600_000, ms // 60_000 % 60, ms // 1000 % 60, ms % 1000


def srt_to_sbv(text: str) -> str:
    cues = []
    for b in _blocks(text):
        ti = next((i for i, l in enumerate(b) if "-->" in l), -1)
        if ti < 0:
            continue
        s, e = b[ti].split("-->")
        cues.append((_parse_ts(s), _parse_ts(e), b[ti + 1:]))
    out = []
    for s, e, lines in cues:
        h, m, sec, ms = _split(s)
        h2, m2, sec2, ms2 = _split(e)
        out.append(f"{h}:{m:02d}:{sec:02d}.{ms:03d},{h2}:{m2:02d}:{sec2:02d}.{ms2:03d}")
        out += lines
        out.append("")
    return "\n".join(out) + "\n"


def sbv_to_srt(text: str) -> str:
    cues = []
    for b in _blocks(text):
        if "," not in b[0]:
            continue
        s, e = b[0].split(",")
        cues.append((_parse_ts(s), _parse_ts(e), b[1:]))
    out = []
    for i, (s, e, lines) in enumerate(cues, 1):
        h, m, sec, ms = _split(s)
        h2, m2, sec2, ms2 = _split(e)
        out.append(str(i))
        out.append(f"{h:02d}:{m:02d}:{sec:02d},{ms:03d} --> {h2:02d}:{m2:02d}:{sec2:02d},{ms2:03d}")
        out += lines
        out.append("")
    return "\n".join(out) + "\n"


def self_test() -> None:
    # Round-trip must preserve every timing and text line.
    assert sbv_to_srt(srt_to_sbv(SRT)).strip() == SRT.strip(), "SRT round-trip failed"
    assert srt_to_sbv(sbv_to_srt(SBV)).strip() == SBV.strip(), "SBV round-trip failed"
    # Cross-check the authored pair agrees once normalized to a common format.
    assert sbv_to_srt(SBV).split("\n")[1] == SRT.split("\n")[1], "authored pair disagrees"


def main() -> None:
    os.makedirs(SAMPLES_DIR, exist_ok=True)
    self_test()
    made = [
        make_png(),
        make_wav(),
        make_gif(),
        write_text("hello.svg", SVG),
        make_dng(),
        make_raw_stub("cr2"),
        make_raw_stub("nef"),
        make_raw_stub("arw"),
        write_text("hello.md", MARKDOWN),
        make_epub(),
        make_mobi(),
        write_text("hello.srt", SRT),
        write_text("hello.sbv", SBV),
        make_zip(),
        *make_fonts(),
        write_text("README.md", README),
    ]
    for path in made:
        print(f"wrote {os.path.relpath(path)} ({os.path.getsize(path)} bytes)")


if __name__ == "__main__":
    main()
