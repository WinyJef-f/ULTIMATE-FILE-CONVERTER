# Sample files

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
