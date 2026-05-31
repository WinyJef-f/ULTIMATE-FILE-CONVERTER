import Foundation

enum Tool: String, CaseIterable {
    case ffmpeg
    case pandoc
    case soffice
    case sevenZip = "7z"
    case calibre = "ebook-convert"
    case fontforge = "fontforge"
    case cp           // /bin/cp, used for Experimental-mode file copies
    case native       // Sentinel: dispatched in-process to NativeConverter, no subprocess

    var executableName: String { rawValue }

    /// Returns the absolute path to the tool's executable, or nil if not available.
    ///
    /// Lookup order:
    ///  1. Homebrew / system paths (primary install method — on-demand via brew)
    ///  2. /Applications/<App>.app (for GUI cask installs: LibreOffice, Calibre)
    ///  3. Resources/bin inside the .app (for developer builds that still bundle tools)
    ///
    /// `.native` returns a sentinel — ToolRunner short-circuits it before launching a process.
    func resolveExecutablePath() -> String? {
        if self == .native { return "<native>" }

        let fm = FileManager.default

        // 1. Homebrew and system paths
        for dir in Self.searchPaths {
            let candidate = "\(dir)/\(executableName)"
            if fm.isExecutableFile(atPath: candidate) { return candidate }
        }

        // 2. GUI cask installs in /Applications
        for path in applicationsBundlePaths {
            if fm.isExecutableFile(atPath: path) { return path }
        }

        // 3. Tools bundled inside the .app (backward compat / dev builds)
        if let bundled = bundledExecutablePath(), fm.isExecutableFile(atPath: bundled) {
            return bundled
        }

        return nil
    }

    /// Executables that live inside a .app in /Applications (LibreOffice, Calibre cask installs).
    private var applicationsBundlePaths: [String] {
        switch self {
        case .soffice:
            return ["/Applications/LibreOffice.app/Contents/MacOS/soffice"]
        case .calibre:
            return ["/Applications/calibre.app/Contents/MacOS/ebook-convert"]
        case .fontforge:
            return ["/Applications/FontForge.app/Contents/MacOS/FontForge"]
        default:
            return []
        }
    }

    private func bundledExecutablePath() -> String? {
        guard let resources = Bundle.main.resourceURL else { return nil }
        switch self {
        case .soffice:
            return resources.appendingPathComponent("LibreOffice.app/Contents/MacOS/soffice").path
        case .cp, .native:
            return nil
        default:
            return resources.appendingPathComponent("bin/\(executableName)").path
        }
    }

    private static let searchPaths: [String] = [
        "/opt/homebrew/bin",   // Apple Silicon Homebrew
        "/usr/local/bin",      // Intel Homebrew
        "/usr/bin",
        "/bin",
    ]
}
