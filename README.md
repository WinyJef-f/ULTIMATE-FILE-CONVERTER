<div align="center">
  <img src="tools/logo.svg" width="140" alt="ULTIMATE-FILE-CONVERTER">
  <h1>ULTIMATE-FILE-CONVERTER</h1>
  <p><b>Drop any file. Pick any format. Get it converted &mdash; locally on macOS and Windows.</b></p>
  <p>
    <a href="https://github.com/WinyJef-f/ULTIMATE-FILE-CONVERTER/releases/latest">
      <img src="https://img.shields.io/badge/download-DMG-0f766e?style=for-the-badge" alt="Download">
    </a>
  </p>
</div>

---

A universal desktop app for converting files between dozens of formats &mdash; **fully on-device, no uploads**. The macOS app is SwiftUI, and the Windows port is native C# WinUI 3. ULTIMATE-FILE-CONVERTER orchestrates a handful of best-in-class open-source tools (FFmpeg, ImageMagick, Pandoc, LibreOffice, &hellip;) behind clean platform-native interfaces.

## Features

- **Drag-and-drop batch conversion.** Drop one file or fifty.
- **30+ formats** across images, audio, video, documents, spreadsheets, presentations, subtitles.
- **Smart routing.** The app picks the right tool for each (source, target) pair automatically.
- **Persistent history** of past conversions, survives across launches.
- **Configurable quality** &mdash; image quality, audio bitrate, video CRF, output folder.
- **Experimental mode** (opt-in) &mdash; raw-byte fallback that turns *any* file into *any other* file. Results are unpredictable. That&rsquo;s the point.
- **Real cancellation** &mdash; hit Cancel mid-batch and the running ffmpeg actually dies.
- **Selectable, copyable error messages** for when something goes sideways.

## Installation

### macOS

1. Download the latest **`.dmg`** from [Releases](https://github.com/WinyJef-f/ULTIMATE-FILE-CONVERTER/releases/latest)
2. Open the DMG and double-click the **`.pkg`** inside
3. Click through the installer &mdash; that's it
4. Open **ULTIMATE-FILE-CONVERTER** from your Applications folder

### Windows

1. Download **`ULTIMATE-FILE-CONVERTER-Setup-<version>-x64.exe`** from [Releases](https://github.com/WinyJef-f/ULTIMATE-FILE-CONVERTER/releases/latest).
2. Run the installer on Windows 10 22H2+ / Windows 11. The app installs to Program Files and adds a Start Menu shortcut.
3. Launch **ULTIMATE-FILE-CONVERTER**. On first run a setup dialog offers to install the conversion tools with **winget**: FFmpeg, ImageMagick, MuPDF, Pandoc, and LibreOffice. Windows may prompt for permission per package.
4. If a tool was installed while the app was open and isn't picked up, restart the app, then convert files normally.

**Dependency setup differs by platform.** The macOS installer bundles the CLI tools inside the `.app`. The Windows app installs tools with winget on first run (re-runnable anytime from the toolbar). Image, SVG, and PDF rasterization use platform-appropriate engines: macOS native frameworks (CGImage / NSImage / CGPDFDocument) and ImageMagick + MuPDF on Windows.

The app is not notarized, so on first launch macOS may show a Gatekeeper warning. Right-click &rarr; Open the first time, or run `xattr -dr com.apple.quarantine /Applications/ULTIMATE-FILE-CONVERTER.app`.

## Supported formats

| Category | Formats |
|---|---|
| Image | JPEG, PNG, WebP, HEIC, AVIF, GIF, BMP, TIFF, SVG, ICO |
| Audio | MP3, WAV, FLAC, AAC, M4A, OGG, OPUS, AIFF |
| Video | MP4, MOV, MKV, WebM, AVI |
| Documents | PDF, DOCX, DOC, ODT, RTF, HTML, Markdown, EPUB, TXT, LaTeX |
| Spreadsheets | XLSX, ODS, CSV |
| Presentations | PPTX, ODP |
| Subtitles | SRT, ASS/SSA, WebVTT, SBV |

With Experimental mode on, every category can be coerced into every other category via raw-byte reinterpretation.

## Building from source

### macOS build

Requires macOS 13+, Xcode, Homebrew, and [xcodegen](https://github.com/yonaskolb/XcodeGen).

```bash
git clone https://github.com/WinyJef-f/ULTIMATE-FILE-CONVERTER.git
cd ULTIMATE-FILE-CONVERTER

# Generate the Xcode project from project.yml
brew install xcodegen
xcodegen generate

# Open in Xcode and ⌘R to build & run
open ULTIMATE-FILE-CONVERTER.xcodeproj

# Or build the distributable DMG (Release config, .pkg + .dmg in dist/)
./tools/build-installer.sh

# Regenerate the app icon from tools/logo.svg
./tools/build-icon.sh
```

### Windows build

Requires Windows 10 22H2+ or Windows 11, the **.NET 8 SDK**, and **Visual Studio 2022** with the **.NET Desktop** and **Windows App SDK / WinUI** components. The build script publishes with Visual Studio's MSBuild (which provides WinUI's `resources.pri` tooling that the bare `dotnet` CLI lacks). **Inno Setup 6** is required to build the `.exe` installer.

```powershell
git clone https://github.com/WinyJef-f/ULTIMATE-FILE-CONVERTER.git
cd ULTIMATE-FILE-CONVERTER

# Publishes the app and builds the EXE installer (Inno Setup required).
pwsh ./tools/windows/build-installers.ps1 -Configuration Release
```

Outputs land in `dist/windows/`: the published app under `publish/` and the Inno Setup `.exe` under `setup/`. The app project is `Windows/UltimateFileConverter.WinUI/UltimateFileConverter.WinUI.csproj` (opened via `ULTIMATE-FILE-CONVERTER.sln`); the installer script is `tools/windows/installer/setup.iss`. Regenerate the Windows icon from the existing renders with `python tools/windows/build-ico.py`.

## Architecture

- **SwiftUI** front-end, macOS 13+ minimum deployment.
- **WinUI 3 / C#** front-end for Windows 10 22H2+ and Windows 11, built as an unpackaged Windows App SDK app distributed as an Inno Setup `.exe`.
- **`ConversionRouter`** maps `(FileKind source, FileKind target, ConversionSettings)` to a sequence of CLI invocations (`ConversionStep[]`). The Windows router mirrors the macOS one branch-for-branch, so both platforms agree on which targets are valid for a given source. It supports multi-step pipelines (e.g.&nbsp;Experimental mode renders raw bytes to an intermediate PNG via ffmpeg, then transcodes to the final image format via magick).
- **`ToolRunner`** executes each step via Foundation&rsquo;s `Process` on macOS and `System.Diagnostics.Process` on Windows, with cancellation that terminates the child process (and its tree on Windows).
- **`AppViewModel`** (macOS) / **`MainViewModel`** (Windows) owns the queue, settings, and persistent history. macOS persists settings in `UserDefaults` and history as JSON in `~/Library/Application Support/ULTIMATE-FILE-CONVERTER/`; Windows persists both as JSON in `%LOCALAPPDATA%\ULTIMATE-FILE-CONVERTER\`.
- **Tools are not bundled on Windows.** macOS bundles tools inside the `.app`. Windows uses winget on first run to install FFmpeg, ImageMagick, MuPDF, Pandoc, LibreOffice, and 7-Zip, avoiding third-party binary redistribution inside the installer.

## What does the heavy lifting

| Tool | Purpose | License |
|---|---|---|
| [FFmpeg](https://ffmpeg.org) | Audio, video, and cross-media conversions | LGPL 2.1+ |
| [LibreOffice](https://www.libreoffice.org) (headless) | Office formats and PDF export | MPL 2.0 |
| [Pandoc](https://pandoc.org) | Markup-to-markup document conversion | GPL 2+ |
| Apple&rsquo;s CGImage / NSImage / CGPDFDocument | macOS image, SVG, and PDF rasterization | Apple system frameworks |
| [ImageMagick](https://imagemagick.org) | Windows image conversion plus SVG and PDF rasterization | ImageMagick license |
| [Ghostscript](https://www.ghostscript.com) | Lets ImageMagick read PDFs on Windows (PDF &rarr; image) | AGPL / commercial |

## License

[MIT](LICENSE). See [LICENSE](LICENSE) for full text.

Third-party tools retain their own licenses. ULTIMATE-FILE-CONVERTER does not redistribute them inside the Windows installer &mdash; they are installed independently via Homebrew (macOS) or winget (Windows) on the user&rsquo;s machine.
