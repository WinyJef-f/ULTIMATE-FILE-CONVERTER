<div align="center">
  <img src="tools/logo.svg" width="140" alt="ULTIMATE-FILE-CONVERTER">
  <h1>ULTIMATE-FILE-CONVERTER</h1>
  <p><b>Drop any file. Pick any format. Get it converted &mdash; entirely on your Mac.</b></p>
  <p>
    <a href="https://github.com/WinyJef-f/ULTIMATE-FILE-CONVERTER/releases/latest">
      <img src="https://img.shields.io/badge/download-DMG-0f766e?style=for-the-badge" alt="Download">
    </a>
  </p>
</div>

---

A native macOS app for converting files between dozens of formats &mdash; **fully on-device, no uploads**. ULTIMATE-FILE-CONVERTER orchestrates a handful of best-in-class open-source tools (FFmpeg, ImageMagick, Pandoc, LibreOffice, &hellip;) behind one clean SwiftUI interface.

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

1. Download the latest **`.dmg`** from [Releases](https://github.com/WinyJef-f/ULTIMATE-FILE-CONVERTER/releases/latest)
2. Open the DMG and double-click the **`.pkg`** inside
3. Click through the installer &mdash; that's it
4. Open **ULTIMATE-FILE-CONVERTER** from your Applications folder

**Completely self-contained.** No Homebrew, no terminal commands, no extra downloads. Everything the app needs &mdash; FFmpeg, LibreOffice, Pandoc &mdash; ships inside the `.app`. Image, SVG, and PDF rasterization use macOS&rsquo;s built-in frameworks (CGImage / NSImage / CGPDFDocument).

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

## Architecture

- **SwiftUI** front-end, macOS 13+ minimum deployment.
- **`ConversionRouter`** maps `(FileKind source, FileKind target, ConversionSettings)` to a sequence of CLI invocations (`ConversionStep[]`). Supports multi-step pipelines (e.g.&nbsp;SVG &rarr; intermediate PNG via rsvg-convert &rarr; AVIF via magick).
- **`ToolRunner`** executes each step via Foundation&rsquo;s `Process`, with `withTaskCancellationHandler` so Cancel actually terminates the child process.
- **`AppViewModel`** owns the queue, settings, and persistent history. Settings persist in `UserDefaults`; history is JSON in `~/Library/Application Support/ULTIMATE-FILE-CONVERTER/history.json`.
- **Tools are not bundled.** The app calls into whatever Homebrew installed. This keeps the app tiny (~1 MB) and avoids redistribution complications. The installer handles dependency setup.

## What does the heavy lifting

| Tool | Purpose | License |
|---|---|---|
| [FFmpeg](https://ffmpeg.org) | Audio, video, and cross-media conversions | LGPL 2.1+ |
| [LibreOffice](https://www.libreoffice.org) (headless) | Office formats and PDF export | MPL 2.0 |
| [Pandoc](https://pandoc.org) | Markup-to-markup document conversion | GPL 2+ |
| Apple&rsquo;s CGImage / NSImage / CGPDFDocument | Image, SVG, and PDF rasterization | Apple system frameworks |

## License

[MIT](LICENSE). See [LICENSE](LICENSE) for full text.

Bundled third-party tools retain their own licenses. ULTIMATE-FILE-CONVERTER does not redistribute them &mdash; they are installed independently via Homebrew on the user&rsquo;s machine.
