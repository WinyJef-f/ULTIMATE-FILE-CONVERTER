## What's new in v1.1.0 — The Works

Six new conversion categories, a stats dashboard, automatic update checks, retry on failure, and full macOS/Windows parity for the tools setup screen.

---

### Added

#### Conversion stats dashboard (both platforms)
A new dashboard — accessible from the toolbar chart icon — shows total files converted, bytes processed, success/failure split, most-used formats, and top conversion pairs. Stats persist across sessions and update in real time.

#### Subtitle conversion: SRT ↔ ASS ↔ VTT ↔ SBV (both platforms)
New `subtitle` category converts between SubRip, SSA/ASS, WebVTT, and SBV. SBV is bridged through SRT in-process (FFmpeg's native SBV support is unreliable), so all four formats convert to all four. No new external dependency — FFmpeg handles the rest.

#### Archive conversion: ZIP ↔ 7z ↔ TAR ↔ TAR.GZ (both platforms)
New `archive` category via 7-Zip: extract-then-recompress pipeline, with a three-step plan for TAR.GZ (extract → intermediate `.tar` → gzip). Compound `.tar.gz` extension handled correctly throughout — output is `file.zip`, not `file.tar.zip`.

#### RAW photo support: CR2, NEF, ARW, DNG (both platforms)
CR2, NEF, ARW, and DNG appear as source-only formats (never a conversion target). macOS decodes via Apple's Image I/O framework natively; Windows routes through ImageMagick which ships with built-in LibRaw/dcraw support. Quality is forced to 100 — no lossy degradation.

#### Kindle e-book formats: AZW3 and MOBI (both platforms)
AZW3 and MOBI join the document category via Calibre's `ebook-convert`. Converts to and from EPUB, DOCX, HTML, Markdown, and the other supported document formats. Calibre is installed on demand (Homebrew / winget) — not bundled, to keep installer size down.

#### Font conversion: TTF ↔ OTF ↔ WOFF ↔ WOFF2 (both platforms)
New `font` category via FontForge. All four web and desktop font formats convert to all four. FontForge handles the TTF↔OTF outline conversion (quadratic ↔ cubic) internally. Installed on demand — `brew install fontforge` / `winget install FontForge.FontForge`.

#### Retry on failure (both platforms)
Failed queue items now show a retry button. The item re-runs with the same settings without re-adding the file.

#### Silent update check on launch (both platforms)
On each launch the app silently polls the GitHub Releases API. If a newer version is available, a non-blocking alert appears with a link to the release page. No telemetry — only the latest release tag is fetched.

#### macOS: Conversion Tools toolbar button
A wrench icon in the macOS toolbar opens the Conversion Tools sheet at any time — not just on first launch. Matches the Windows behaviour where the wrench icon has always been accessible from the toolbar.

---

### Changed

- **macOS setup sheet** retitled "Conversion Tools" (was "First-Run Setup") to reflect that it is now accessible at any time, not only on first launch.
- **macOS setup sheet** "Skip" button renamed to "Cancel".
- **README** gained a "Why these dependencies?" section — what each tool is, why install-on-demand is safe, and how the install is verified (Homebrew / winget signature checking).
- **Supported format count** updated throughout (installer welcome screens, README) to reflect 40+ formats across 9 categories.

---

### Removed

Nothing removed.

---

### Fixed

- **Windows `WeirdRoute`** now lists all non-image/audio/video categories explicitly instead of using a silent fallthrough (`_ =>`), matching the macOS pattern. No behaviour change — purely a robustness improvement for future category additions.

---

### New dependencies (install on demand — not bundled)

| Tool | macOS (Homebrew) | Windows (winget) | Purpose |
|---|---|---|---|
| 7-Zip | `p7zip` | `7zip.7zip` | Archive conversion |
| LibreOffice | `--cask libreoffice` | `TheDocumentFoundation.LibreOffice` | Office format + PDF export |
| Calibre | `--cask calibre` | `calibre.calibre` | E-book conversion |
| FontForge | `fontforge` | `FontForge.FontForge` | Font format conversion |

FFmpeg and Pandoc were already installed in v1.0.x. ImageMagick and MuPDF are Windows-only (macOS uses native frameworks for image and PDF work).

---

## Install — macOS

1. Download **`ULTIMATE-FILE-CONVERTER-1.1.0.dmg`** below
2. Open the DMG, double-click the `.pkg` inside
3. Click through the installer — that's it
4. Open **ULTIMATE-FILE-CONVERTER** from your Applications folder; the Conversion Tools sheet will offer to install any missing tools via Homebrew

> Not notarized. First launch may show a Gatekeeper warning — right-click → Open the first time.

## Install — Windows

1. Download **`ULTIMATE-FILE-CONVERTER-Setup-1.1.0-x64.exe`** below
2. Run the installer and click through — installs to Program Files and adds a Start Menu shortcut
3. Open **ULTIMATE-FILE-CONVERTER**; click the wrench icon to install conversion tools via winget

The app installs its external tools via **winget** on demand, with your permission. An internet connection is required that one time.

> Windows 10 22H2 (build 19045) or newer, x64 required.

## Requirements — macOS

- macOS 13 (Ventura) or newer
- Apple Silicon or Intel Mac
- ~700 MB free disk after tool installation (most of that is LibreOffice)

## Requirements — Windows

- Windows 10 22H2 (build 19045) or newer, x64
- ~1.5 GB free disk after full tool installation
- Internet connection required on first run to install tools via winget

## License

[MIT](https://github.com/WinyJef-f/ULTIMATE-FILE-CONVERTER/blob/main/LICENSE). All conversion tools (FFmpeg, Pandoc, LibreOffice, 7-Zip, Calibre, FontForge, ImageMagick, MuPDF) are free and open-source; they are installed separately and remain subject to their own licenses. None were modified.
