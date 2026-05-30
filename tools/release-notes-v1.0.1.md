**v1.0.1 is the version you should download.** v1.0.0 had a blank app icon and required Homebrew during install — both fixed. This release adds a Windows installer for the first time.

## What changed

- **The macOS installer is now completely self-contained.** No Homebrew. No terminal. No "go install X first" prompts. Download the DMG, run the installer inside, the app is ready to use. Every tool the app needs ships inside the `.app`.
- **App icon shows up correctly** in Finder, Dock, and the About window. v1.0.0 had `CFBundleIconFile` set in the wrong way for xcodebuild to honor — fixed by switching to an Asset Catalog.
- **Installer license/readme text is readable in dark mode.** Previously the code-block backgrounds were nearly white-on-light because the pages inherited macOS dark mode. Forced to light mode with explicit colors.
- **DMG is ~350 MB** — bigger than v1.0.0's 343 KB because everything is now bundled (LibreOffice alone is ~300 MB compressed). In exchange you get a true one-and-done install.
- **Windows port added.** WinUI 3 app with full feature parity: drag-and-drop, batch conversion, conversion history, and settings persistence. Ships as both an Inno Setup EXE and a Windows Installer MSI.

## Install — macOS

1. Download **`ULTIMATE-FILE-CONVERTER-1.0.1.dmg`** below
2. Open the DMG, double-click the `.pkg` inside
3. Click through the installer — that's it
4. Open **ULTIMATE-FILE-CONVERTER** from your Applications folder and convert files

> Not notarized. First launch may show a Gatekeeper warning — right-click → Open the first time.

## Install — Windows

1. Download **`ULTIMATE-FILE-CONVERTER-Setup-1.0.1-x64.exe`** (recommended) or the **`.msi`** below
2. Run the installer and click through — it installs to Program Files and adds a Start Menu shortcut
3. Open **ULTIMATE-FILE-CONVERTER** and convert files

The app installs its external tools (FFmpeg, ImageMagick, Pandoc, LibreOffice, etc.) via **winget** on first run, with your permission. An internet connection is required that one time.

> Windows 10 22H2 (x64) or newer required. ARM64 is not supported in this release.

## Under the hood — macOS

| Tool | Where | What it does |
|---|---|---|
| **FFmpeg** | Bundled inside the .app | Audio, video, cross-media |
| **LibreOffice** (headless) | Bundled inside the .app | DOCX, XLSX, PPTX, ODT, PDF export |
| **Pandoc** | Bundled inside the .app | Markdown / HTML / DOCX / EPUB / RTF / LaTeX |
| **macOS CGImage / NSImage / CGPDFDocument** | System framework | Image, SVG, PDF rasterization — no external tool needed |

ImageMagick, librsvg, and Ghostscript were dropped in favor of macOS's built-in image frameworks. This made the bundle smaller and more reliable (no config-file paths to chase).

## Under the hood — Windows

| Tool | Installed via | What it does |
|---|---|---|
| **FFmpeg** | winget | Audio, video, cross-media |
| **ImageMagick** | winget | Image conversion and processing |
| **Pandoc** | winget | Markdown / HTML / DOCX / EPUB / RTF / LaTeX |
| **LibreOffice** | winget | DOCX, XLSX, PPTX, ODT, PDF export |
| **MuPDF (mutool)** | winget | PDF to image rasterization |
| **7-Zip** | winget | Archive extraction and creation |

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
