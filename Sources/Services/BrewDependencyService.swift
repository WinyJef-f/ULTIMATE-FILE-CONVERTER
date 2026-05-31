import Foundation

// MARK: - BrewDependency

/// A third-party CLI tool the app relies on, plus how to locate and install it via Homebrew.
struct BrewDependency: Identifiable {
    /// Human-readable name shown in setup UI.
    let friendlyName: String
    /// Homebrew formula argument(s). Examples: "ffmpeg", "--cask libreoffice".
    let formula: String
    /// The `Tool` enum case used to probe whether the tool is installed.
    let tool: Tool
    /// One-sentence description of what the tool provides.
    let purpose: String

    var id: String { friendlyName }

    /// True when the tool's executable can be found (bundled, Homebrew, or system).
    var isInstalled: Bool {
        tool.resolveExecutablePath() != nil
    }
}

// MARK: - BrewDependencyService

/// First-run setup: locates and installs required conversion tools via Homebrew.
///
/// Mirrors `DependencyService` on Windows (which uses winget). One failed package
/// does not abort the rest — the user gets as many working tools as possible.
enum BrewDependencyService {

    // MARK: Dependencies

    /// All required third-party tools, in install order.
    static let required: [BrewDependency] = [
        BrewDependency(
            friendlyName: "FFmpeg",
            formula: "ffmpeg",
            tool: .ffmpeg,
            purpose: "Audio, video, and cross-media conversions."
        ),
        BrewDependency(
            friendlyName: "Pandoc",
            formula: "pandoc",
            tool: .pandoc,
            purpose: "Markup document conversions (Markdown, HTML, DOCX, EPUB, …)."
        ),
        BrewDependency(
            friendlyName: "7-Zip",
            formula: "p7zip",
            tool: .sevenZip,
            purpose: "Archive conversion (ZIP, 7z, TAR, TAR.GZ)."
        ),
        BrewDependency(
            friendlyName: "LibreOffice",
            formula: "--cask libreoffice",
            tool: .soffice,
            purpose: "Office formats and PDF export."
        ),
        BrewDependency(
            friendlyName: "Calibre",
            formula: "--cask calibre",
            tool: .calibre,
            purpose: "E-book format conversions (EPUB, MOBI, AZW3)."
        ),
        BrewDependency(
            friendlyName: "FontForge",
            formula: "fontforge",
            tool: .fontforge,
            purpose: "Font format conversions (TTF, OTF, WOFF, WOFF2)."
        ),
    ]

    // MARK: Status

    /// Dependencies whose executables cannot currently be found.
    static var missing: [BrewDependency] {
        required.filter { !$0.isInstalled }
    }

    /// True when every required tool is present.
    static var allInstalled: Bool {
        required.allSatisfy { $0.isInstalled }
    }

    // MARK: Marker file

    /// Path to the sentinel file written after a successful setup run.
    private static var markerURL: URL {
        let appSupport = FileManager.default
            .urls(for: .applicationSupportDirectory, in: .userDomainMask)
            .first!
        return appSupport
            .appendingPathComponent("ULTIMATE-FILE-CONVERTER")
            .appendingPathComponent(".setup-done")
    }

    /// True when the marker file exists on disk.
    static var markerExists: Bool {
        FileManager.default.fileExists(atPath: markerURL.path)
    }

    /// Writes (or overwrites) the marker file with the current UTC timestamp.
    static func writeMarker() {
        do {
            let dir = markerURL.deletingLastPathComponent()
            try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
            let timestamp = ISO8601DateFormatter().string(from: Date())
            try timestamp.write(to: markerURL, atomically: true, encoding: .utf8)
        } catch {
            // Non-fatal: setup still succeeded even if we cannot write the marker.
        }
    }

    // MARK: First-run gate

    /// True when setup UI should be offered: marker absent **and** at least one tool is missing.
    static var shouldOfferFirstRunSetup: Bool {
        !markerExists && !allInstalled
    }

    // MARK: Brew location

    /// Returns the absolute path to `brew`, or `nil` when Homebrew is not installed.
    static func findBrew() -> String? {
        let candidates = [
            "/opt/homebrew/bin/brew",   // Apple Silicon
            "/usr/local/bin/brew",      // Intel
        ]
        return candidates.first {
            FileManager.default.isExecutableFile(atPath: $0)
        }
    }

    // MARK: Installation

    /// Installs every missing dependency using `brew install` / `brew install --cask`.
    ///
    /// - Parameter log: Called on each status line as installation progresses.
    /// - Returns: `true` if all required tools are present after the run.
    @discardableResult
    static func installMissingAsync(log: @escaping (String) -> Void) async -> Bool {
        let toInstall = missing

        guard !toInstall.isEmpty else {
            log("All conversion tools are already installed.")
            writeMarker()
            return true
        }

        guard let brew = findBrew() else {
            log("Homebrew was not found. Install it from https://brew.sh, then run setup again.")
            return false
        }

        log("Installing \(toInstall.count) tool(s) with Homebrew. This may take a few minutes.")

        for dep in toInstall {
            // Re-check in case a previous iteration made it available (e.g. cask dependency).
            if dep.isInstalled {
                log("✓ \(dep.friendlyName) is already installed.")
                continue
            }

            log("Installing \(dep.friendlyName) (\(dep.formula))…")

            // Split "install" + formula tokens (handles "--cask libreoffice" → ["--cask", "libreoffice"]).
            let formulaArgs = dep.formula.split(separator: " ").map(String.init)
            let arguments = ["install"] + formulaArgs

            do {
                let result = try await ToolRunner.run(executable: brew, arguments: arguments)

                if dep.isInstalled {
                    log("✓ \(dep.friendlyName) installed.")
                } else {
                    let detail = firstMeaningfulLine(result.stderr)
                               ?? firstMeaningfulLine(result.stdout)
                               ?? ""
                    let suffix = detail.isEmpty ? "" : " \(detail)"
                    log("✗ \(dep.friendlyName) did not install (brew exit \(result.exitCode)).\(suffix)")
                }
            } catch {
                log("✗ \(dep.friendlyName): could not start brew (\(error.localizedDescription)).")
                // Best-effort: continue with remaining deps.
            }
        }

        let ok = allInstalled
        if ok {
            writeMarker()
            log("All conversion tools are ready.")
        } else {
            let stillMissing = missing.map(\.friendlyName).joined(separator: ", ")
            log("Setup finished with some tools still missing: \(stillMissing). "
                + "You can rerun setup, or install them manually, then restart the app.")
        }
        return ok
    }

    // MARK: Helpers

    private static func firstMeaningfulLine(_ text: String) -> String? {
        guard !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return nil }
        for line in text.components(separatedBy: "\n") {
            let trimmed = line.trimmingCharacters(in: .whitespacesAndNewlines)
            if !trimmed.isEmpty { return trimmed }
        }
        return nil
    }
}
