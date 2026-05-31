# ULTIMATE-FILE-CONVERTER — Feature Roadmap

A staged plan for nine requested features, ordered **easiest → hardest** to implement.
Every stage ships on **both macOS (SwiftUI) and Windows (WinUI 3 / C#)** before it is
considered done. Stages are independent unless noted; the order reflects implementation
effort and risk, not strict dependency.

## How the app is built (recap)

- **`ConversionRouter`** maps `(FileKind source, FileKind target, ConversionSettings)` to a
  `ConversionPlan` (a list of `ConversionStep`s with `{INPUT}` / `{OUTPUT}` / `{OUTPUT_DIR}`
  argument templates). The Swift and C# routers mirror each other branch-for-branch.
- **`FileKind` / `FileCategory`** enumerate formats and their families. `validTargets()` /
  `canConvert()` probe the router to drive the UI's target picker.
- **`Tool`** enumerates external CLIs. macOS **bundles** binaries inside the `.app`
  (`tools/bundle-tools.sh`, resolved by `Tool.swift`); Windows **installs via winget** on
  first run (`Services/DependencyService.cs`, resolved by `Engine/ToolRunner.cs`).
- **`FormatDetector`** identifies a file by extension first, then by content sniffing.
- History is already persisted as JSON on both platforms (`HistoryEntry`,
  `HistoryStore`/`AppViewModel`).

## Cross-cutting checklist (applies to every stage that adds a format)

When a stage introduces a new format or category, touch all of these to keep parity:

1. **Models** — add cases to `FileKind` + map to `FileCategory` (Swift **and** C#), with
   `displayName`, `recognizedExtensions`, `canonicalExtension`. New categories also need a
   display name + an SF Symbol (macOS) and a Segoe Fluent glyph (Windows).
2. **Router** — add the routing branch to **both** `ConversionRouter.swift` and
   `ConversionRouter.cs`, in the same position, so `validTargets` matches across platforms.
3. **FormatDetector** — confirm extension/content detection covers the new kinds.
4. **Tool wiring** — if a new CLI is needed: add to `Tool` enum (both), to macOS
   `bundle-tools.sh` + `Tool.swift` resolution, and to Windows `DependencyService` (winget id)
   + `ToolRunner` search roots.
5. **UI** — category badge/glyph, target-picker grouping (usually automatic once the router
   reports the new targets).
6. **Docs/installer** — update the README "Supported formats" + "What does the heavy lifting"
   tables; update installer size notes (macOS) / winget package list (Windows).

---

## Stage 1 — Conversion stats dashboard  *(easiest)*  ✅ Shipped

**What:** A dashboard showing files converted, total bytes processed, and most-used
formats/pairs, persistent across sessions.

**Why easiest:** No external tools, no new formats, no routing changes. History is already
persisted on both platforms — this is aggregation + a new view.

- **Model:** add a `bytesProcessed` (input size) field to `HistoryEntry` on both platforms;
  capture it at `finalize()` time in `AppViewModel`/`MainViewModel`. Old entries without the
  field default to 0 (back-compat on the JSON read).
- **Aggregation:** total count, success/failure split, summed bytes, top source/target
  formats, top conversion pairs. Pure in-memory reduce over `history`.
- **UI:** a new dashboard sheet/page — `Views/DashboardView.swift` (SwiftUI) and
  `Views/DashboardDialog.xaml` (WinUI), opened from the toolbar near History.
- **Risk:** low. Main subtlety is back-compatible history JSON and counting reruns of the
  same file correctly.

## Stage 2 — Subtitle conversion: SRT ↔ ASS ↔ VTT ↔ SBV  ✅ Shipped

**What:** A new `subtitle` category converting between SubRip, SSA/ASS, WebVTT, and SBV.

**How it shipped:** New `FileCategory.subtitle` and `FileKind`s `srt`/`ass`/`vtt`/`sbv` on both
platforms. The router's `subtitle → subtitle` branch runs srt/ass/vtt straight through
`ffmpeg -y -i {INPUT} {OUTPUT}`; SBV is bridged through SRT by an in-process converter
(`SubtitleConverter`, dispatched via the macOS `.native` tool and the Windows `Tool.Subtitle`
sentinel), so SBV→SRT→{ass,vtt} and {ass,vtt}→SRT→SBV work without relying on ffmpeg's weak
SBV support. No new external dependency. Demo files `samples/hello.srt` and `samples/hello.sbv`
were added.

**Why early:** FFmpeg (already bundled/installed on both platforms) converts srt/ass/vtt
directly — no new dependency for three of the four formats.

- **Models:** new `FileCategory.subtitle`; new `FileKind`s `srt`, `ass`, `vtt`, `sbv`.
- **Router:** `subtitle → subtitle` via `ffmpeg -i {INPUT} {OUTPUT}` for srt/ass/vtt.
- **SBV caveat:** FFmpeg has weak SBV support. Handle SBV with a small in-process converter
  (`native` tool on macOS; a `Tool.Copy`-style internal step on Windows) that translates
  SBV ⇄ SRT, then lets ffmpeg reach ass/vtt. This is the only non-trivial part.
- **Risk:** low–medium. Mostly new-category plumbing; SBV is the one custom path.

## Stage 3 — Archive conversion: ZIP ↔ 7z ↔ tar.gz  ✅ Shipped

**What:** A new `archive` category converting between zip, 7z, tar, and tar.gz via 7-Zip.

**How it shipped:** New `FileCategory.archive` and `FileKind`s `zip`/`sevenz`/`tar`/`targz`
on both platforms. The router's `archive → archive` branch uses a two-step extract-then-recompress
pipeline (`7z x -y {INPUT} -o<dir>` then `7z a -t<fmt> {OUTPUT} <dir>/* -r`). TAR.GZ requires
a three-step plan: extract → create intermediate `.tar` → `7z a -tgzip`. Both platforms
handle the `.tar.gz` compound extension in `FormatDetector` (pre-check before the single-extension
lookup) and in the ViewModel (`GetBaseName`/`baseName` helpers) so output is `file.zip` not
`file.tar.zip`. `tools/bundle-tools.sh` now bundles `7z` alongside ffmpeg/pandoc; Windows
`DependencyService` installs `7zip.7zip` via winget. Demo file `samples/hello.zip` was added.

**Why here:** 7-Zip is already half-wired — macOS `Tool` enum has `sevenZip = "7z"`, and the
README already lists 7-Zip among winget installs. But archive conversion is *not* one-file-in /
one-file-out: it means **extract to a temp dir, then recompress**, so it needs a two-step
pipeline and temp-dir lifecycle.

- **Models:** new `FileCategory.archive`; new `FileKind`s `zip`, `sevenz`, `tar`, `targz`.
- **Tool wiring:** add `Tool.SevenZip` to the Windows `Tool` enum + `DependencyService`
  (`7zip.7zip`) + `ToolRunner` search roots; confirm macOS bundling of `7z` in
  `bundle-tools.sh` (currently only ffmpeg/pandoc are bundled).
- **Router:** multi-step plan — `7z x` into a unique temp dir, then `7z a` / `tar` to the
  target format. tar.gz needs the tar+gzip combination 7-Zip handles in two passes.
- **Risk:** medium. Temp-dir management, nested tar.gz, and path/space handling.

## Stage 4 — RAW photo support: CR2, NEF, ARW, DNG  ✅ Shipped

**What:** Read camera RAW files and convert them to standard images (RAW is source-only).

**How it shipped:** New `FileKind`s `cr2`/`nef`/`arw`/`dng` with `category = .image` and an
`isSourceOnly`/`IsSourceOnly()` flag on both platforms. The router rejects any source-only
kind as a target and routes `source.isSourceOnly && dst == .image` to the native decode path
with quality forced to 100 — no lossy degradation. macOS uses `CGImageSourceCreateWithURL`
which decodes all four RAW formats natively via Apple's RAW frameworks; Windows uses ImageMagick
which ships with built-in LibRaw/dcraw support in current distributions. No new external
dependency on either platform. Retry-on-failure and Check for Updates were also shipped
alongside this stage.

- **Models:** new image `FileKind`s `cr2`, `nef`, `arw`, `dng` (category `image`,
  source-only — never an output target).
- **macOS:** Apple's Image I/O / `CGImageSource` already decodes most RAW formats — extend
  `NativeConverter` to accept RAW inputs; likely **no new dependency**.
- **Windows:** ImageMagick needs a RAW delegate. Add **libraw/dcraw** (winget
  `LibRaw.LibRaw` or bundle `dcraw`) as a new dependency, or a `RAW → intermediate TIFF →
  ImageMagick` two-step.
- **Router:** `RAW(image) → image`, mirroring the existing SVG-source special case.
- **Risk:** medium. Windows dependency choice + color/orientation fidelity.

## Stage 5 — Kindle output via Calibre: EPUB → AZW3/MOBI  ✅ Shipped

**What:** Kindle-compatible output, complementing existing EPUB support, via Calibre's
`ebook-convert` CLI.

**How it shipped:** New document `FileKind`s `azw3` and `mobi` on both platforms. `Tool.Calibre`
/ `Tool.Calibre` added with `ebook-convert` as the executable (winget id `calibre.calibre`).
The router's Calibre branch runs after soffice and handles `calibreFamily → azw3/mobi` and
`azw3/mobi → calibreFamily` with `ebook-convert {INPUT} {OUTPUT}` (Calibre auto-detects
formats from extensions). **Bundling decision:** Calibre (~300–500 MB) is install-on-demand on
both platforms — macOS resolves via Homebrew search paths then `/Applications/calibre.app/Contents/MacOS/`,
Windows installs via winget and searches `Program Files\Calibre2`. This keeps the DMG lean.
Both startup update check (silent launch-time poll) and the quality fix for RAW images were
also finalised alongside this stage.

- **Models:** new document `FileKind`s `azw3`, `mobi`.
- **Tool wiring:** `Tool.Calibre` (`ebook-convert`). Windows winget id `calibre.calibre`;
  macOS searches Homebrew paths then the `.app` bundle in `/Applications`. Not bundled inside
  the macOS `.app` to keep the DMG lightweight.
- **Router:** `ebook-convert {INPUT} {OUTPUT}` for `calibreFamily ↔ {azw3,mobi}`.
- **Risk:** medium (resolved). Dependency size and bundling policy were the main decisions.

## Stage 6 — Font conversion: TTF ↔ OTF ↔ WOFF ↔ WOFF2

**What:** A new `font` category converting between desktop and web font formats.

**Why harder than it looks:** The natural tool, **fonttools**, is a *Python package*, which
does not fit the app's winget/Homebrew-binary dependency model. Options: (a) `fonttools` via
a bundled Python, (b) Google's `woff2` binaries (`woff2_compress`/`woff2_decompress`, brew
`woff2`) plus `sfnt2woff`/`sfnt2woff-zopfli` for WOFF1, or (c) a small bundled Rust/Go helper.

- **Models:** new `FileCategory.font`; new `FileKind`s `ttf`, `otf`, `woff`, `woff2`.
- **Tool wiring:** whichever toolchain we pick must be made installable/bundlable on both
  platforms — this is the bulk of the work.
- **Router:** `font → font`. WOFF2 ⇄ TTF/OTF is straightforward with `woff2`; WOFF1 and
  TTF⇄OTF outline conversion need the extra tools.
- **Risk:** medium–high, driven entirely by packaging the toolchain cross-platform.

## Stage 7 — Right-click "Convert with UFC"

**What:** A shell context-menu entry that launches the app pre-loaded with the selected
file(s).

**Why hard:** OS-level integration plus app changes to accept a launch-time file argument —
the app currently ignores command-line/open-file input on both platforms.

- **Windows:** add `[Registry]` entries in `tools/windows/installer/setup.iss`
  (`HKCR\*\shell\ConvertWithUFC\command = "<app>.exe" "%1"`); implement **single-instance
  activation** so an already-open window enqueues the file (WinUI `AppInstance`
  redirect / `GetActivatedEventArgs`). Today `MainWindow` reads no args.
- **macOS:** declare `CFBundleDocumentTypes` for supported extensions in `project.yml`/
  Info.plist, handle `onOpenURL` / `application(_:open:)` to enqueue files, and add a Finder
  **Services** entry (or rely on "Open With"). `App.swift` currently only has menu commands.
- **Risk:** medium–high. Touches installers and app activation on both platforms; needs the
  "launch with a file" plumbing that Stage 8 also benefits from.

## Stage 8 — CLI interface for power users

**What:** A terminal command to script conversions headlessly (e.g.
`ufc convert in.png out.webp --quality 90`, `ufc list-targets in.heic`).

**Why hard:** Requires a headless execution path and a new build/distribution target. The
conversion engine (`ConversionRouter` + `ToolRunner` + models) is already cleanly separated
from the UI, which makes this feasible, but it is still a new surface.

- **macOS:** add a separate CLI target in `project.yml` (and `build-installer.sh`) that links
  the existing `Engine/` + `Models/` sources; install a `ufc` symlink on `PATH`.
- **Windows:** the WinUI app is a `WinExe` (no console). Extract the engine into a class
  library and add a small **console companion** exe (`ufc.exe`); add its folder to `PATH` in
  the installer. Wire up arg parsing, progress to stdout, and proper exit codes.
- **Risk:** high. New build targets, packaging, PATH setup, and console/GUI subsystem
  handling on Windows.

## Stage 9 — 3D model conversion: OBJ ↔ STL ↔ GLTF  *(hardest)*

**What:** A new `model` category converting between OBJ, STL, and glTF/GLB.

**Why hardest:** The heaviest dependency and the most finicky conversions. **assimp**
(`assimp export`, brew `assimp`) is lighter than Blender and the recommended first choice;
Blender headless (`blender --background --python …`) is the fallback when assimp's glTF
fidelity is insufficient. Either is a large/awkward dependency, and glTF (geometry +
materials + textures, `.gltf` vs binary `.glb`) is the trickiest pairing.

- **Models:** new `FileCategory.model`; new `FileKind`s `obj`, `stl`, `gltf`, `glb`.
- **Tool wiring:** add `Tool.Assimp` (or `Tool.Blender`) on both platforms — bundling Blender
  on macOS is very large, so install-on-demand is likely.
- **Router:** `model → model` via `assimp export {INPUT} {OUTPUT}`; glTF may need texture/
  material side-files handled.
- **Risk:** high. Dependency weight + glTF correctness + asset side-files.

---

## Recommended grouping

- **Quick wins (no/known tools):** Stage 1 (dashboard), Stage 2 (subtitles), Stage 3
  (archives).
- **New single-purpose dependencies:** Stage 4 (RAW), Stage 5 (Kindle), Stage 6 (fonts).
- **New app surfaces / OS integration:** Stage 7 (right-click), Stage 8 (CLI).
- **Heaviest:** Stage 9 (3D).

Stages 7 and 8 share "launch/run with a given file" plumbing; doing right-click first lays
groundwork the CLI reuses.

## Open decisions to settle when we reach each stage

- **Bundling policy for heavy tools** (Calibre, Blender, a font toolchain): keep the macOS
  "bundle everything in the `.app`" model, or move to install-on-demand like Windows? This
  affects DMG size materially.
- **Whether RAW/SBV warrant fidelity tuning** (color profiles, orientation, styling) beyond a
  basic correct conversion.
- **CLI surface**: command names, flags, and whether it shares a config with the GUI.
