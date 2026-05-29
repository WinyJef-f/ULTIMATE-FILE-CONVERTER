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
- **30+ formats** across images, audio, video, documents, spreadsheets, presentations.
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

1. Build or download either **`ULTIMATE-FILE-CONVERTER-Setup-<version>-x64.exe`** or **`ULTIMATE-FILE-CONVERTER-<version>-x64.msi`**.
2. Install it on Windows 10 2004+ / Windows 11.
3. Launch **ULTIMATE-FILE-CONVERTER**. On first run, the WinUI 3 app uses **winget** to install missing conversion tools: FFmpeg, ImageMagick, Pandoc, LibreOffice, and 7-Zip.
4. Restart the app if winget updated your PATH during dependency installation, then convert files normally.

**Dependency setup differs by platform.** The macOS installer can ship or locate the needed CLI tools depending on the release build. The Windows installer stays lean and lets the WinUI 3 app install missing conversion tools with winget on first run. Image, SVG, and PDF rasterization use platform-appropriate engines: macOS native frameworks and ImageMagick/FFmpeg on Windows.

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

Requires Windows 10 2004+ or Windows 11, Visual Studio 2022 with the Windows App SDK / WinUI workload, .NET 8 SDK, WiX Toolset (installed automatically as a .NET tool by the build script), and Inno Setup 6 if you also want the `.exe` installer.

```powershell
git clone https://github.com/WinyJef-f/ULTIMATE-FILE-CONVERTER.git
cd ULTIMATE-FILE-CONVERTER

# Produces dist/windows/exe, dist/windows/msi, and dist/windows/setup when Inno Setup is installed.
pwsh ./tools/windows/build-installers.ps1 -Configuration Release
```

The Windows app project lives at `Windows/UltimateFileConverter.WinUI/UltimateFileConverter.WinUI.csproj`, and the root solution is `ULTIMATE-FILE-CONVERTER.sln`.

## Architecture

- **SwiftUI** front-end, macOS 13+ minimum deployment.
- **WinUI 3 / C#** front-end for Windows 10 2004+ and Windows 11, built as an unpackaged Windows App SDK app for `.exe` and `.msi` distribution.
- **`ConversionRouter`** maps `(FileKind source, FileKind target, ConversionSettings)` to a sequence of CLI invocations (`ConversionStep[]`). Supports multi-step pipelines (e.g.&nbsp;SVG &rarr; intermediate PNG via rsvg-convert &rarr; AVIF via magick).
- **`ToolRunner`** executes each step via Foundation&rsquo;s `Process` on macOS and `System.Diagnostics.Process` on Windows, with cancellation that terminates the child process.
- **`AppViewModel`** owns the queue, settings, and persistent history. Settings persist in `UserDefaults`; history is JSON in `~/Library/Application Support/ULTIMATE-FILE-CONVERTER/history.json`.
- **Tools are not bundled.** macOS calls bundled/Homebrew-installed tools depending on build mode. Windows uses winget on first run to install FFmpeg, ImageMagick, Pandoc, LibreOffice, and 7-Zip, avoiding third-party binary redistribution inside the app installer.

## What does the heavy lifting

| Tool | Purpose | License |
|---|---|---|
| [FFmpeg](https://ffmpeg.org) | Audio, video, and cross-media conversions | LGPL 2.1+ |
| [LibreOffice](https://www.libreoffice.org) (headless) | Office formats and PDF export | MPL 2.0 |
| [Pandoc](https://pandoc.org) | Markup-to-markup document conversion | GPL 2+ |
| Apple&rsquo;s CGImage / NSImage / CGPDFDocument | macOS image, SVG, and PDF rasterization | Apple system frameworks |
| [ImageMagick](https://imagemagick.org) | Windows image conversion and PDF/image rasterization | ImageMagick license |
| [7-Zip](https://www.7-zip.org) | Archive-capable dependency installed for future Windows routes | LGPL / BSD mix |

## License

[MIT](LICENSE). See [LICENSE](LICENSE) for full text.

Bundled third-party tools retain their own licenses. ULTIMATE-FILE-CONVERTER does not redistribute them &mdash; they are installed independently via Homebrew on the user&rsquo;s machine.
