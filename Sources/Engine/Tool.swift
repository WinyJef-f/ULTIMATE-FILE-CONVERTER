import Foundation

enum Tool: String, CaseIterable {
    case ffmpeg
    case pandoc
    case soffice
    case sevenZip = "7z"
    case cp           // /bin/cp, used for Experimental-mode file copies
    case native       // Sentinel: dispatched in-process to NativeConverter, no subprocess

    var executableName: String { rawValue }

    /// Returns the absolute path to the tool's executable, or nil if not available.
    ///
    /// Lookup order:
    ///  1. Inside our own .app bundle (Resources/bin/<tool> or Resources/LibreOffice.app/Contents/MacOS/soffice)
    ///  2. Homebrew and system locations (lets dev builds find tools without re-bundling)
    ///
    /// `.native` returns a sentinel path — ToolRunner short-circuits it before launching a process.
    func resolveExecutablePath() -> String? {
        if self == .native { return "<native>" }

        let fm = FileManager.default

        // 1. Bundled tools shipped inside the .app
        if let bundled = bundledExecutablePath(), fm.isExecutableFile(atPath: bundled) {
            return bundled
        }

        // 2. System / Homebrew fallback
        for dir in Self.searchPaths {
            let candidate = "\(dir)/\(executableName)"
            if fm.isExecutableFile(atPath: candidate) {
                return candidate
            }
        }
        return nil
    }

    private func bundledExecutablePath() -> String? {
        guard let resources = Bundle.main.resourceURL else { return nil }
        switch self {
        case .soffice:
            return resources
                .appendingPathComponent("LibreOffice.app/Contents/MacOS/soffice")
                .path
        case .cp:
            return nil   // /bin/cp is always present on macOS; no point bundling.
        case .native:
            return nil   // Not an executable.
        default:
            return resources
                .appendingPathComponent("bin/\(executableName)")
                .path
        }
    }

    private static let searchPaths: [String] = [
        "/opt/homebrew/bin",   // Apple Silicon Homebrew
        "/usr/local/bin",      // Intel Homebrew
        "/usr/bin",
        "/bin"
    ]
}
