## What changed

- **Fixed Experimental mode formatting in the installer license screen.** The section was rendering with a styled disclaimer box (cream background, orange border) that clashed with the rest of the license page; it now uses plain text matching the other sections.

## Install — macOS

1. Download **`ULTIMATE-FILE-CONVERTER-1.0.2.dmg`** below
2. Open the DMG, double-click the `.pkg` inside
3. Click through the installer — that's it
4. Open **ULTIMATE-FILE-CONVERTER** from your Applications folder and convert files

> Not notarized. First launch may show a Gatekeeper warning — right-click → Open the first time.

## Install — Windows

1. Download **`ULTIMATE-FILE-CONVERTER-Setup-1.0.2-x64.exe`** below
2. Run the installer and click through — it installs to Program Files and adds a Start Menu shortcut
3. Open **ULTIMATE-FILE-CONVERTER** and convert files

The app installs its external tools (FFmpeg, ImageMagick, Pandoc, LibreOffice, etc.) via **winget** on first run, with your permission. An internet connection is required that one time.

> Windows 10 22H2 (x64) or newer required. ARM64 is not supported in this release.

## Requirements — macOS

- macOS 13 (Ventura) or newer
- Apple Silicon or Intel Mac
- ~700 MB free disk (most of that is LibreOffice)
- No internet connection required after install

## Requirements — Windows

- Windows 10 22H2 (build 19045) or newer, x64
- ~1 GB free disk (after tool installation)
- Internet connection required on first run (to install tools via winget)

## License

[MIT](https://github.com/WinyJef-f/ULTIMATE-FILE-CONVERTER/blob/main/LICENSE). macOS bundled tools: FFmpeg (LGPL 2.1+), Pandoc (GPL 2+), LibreOffice (MPL 2.0) — none were modified. Windows tools are installed separately and remain subject to their own licenses.
