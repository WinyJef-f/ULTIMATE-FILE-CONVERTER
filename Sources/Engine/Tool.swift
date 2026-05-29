import Foundation

enum Tool: String, CaseIterable {
    case ffmpeg
    case magick
    case pandoc
    case soffice
    case ghostscript = "gs"
    case sevenZip = "7z"
    case rsvgConvert = "rsvg-convert"
    case cp           // /bin/cp, used for Experimental-mode file copies

    var executableName: String { rawValue }

    private static let searchPaths: [String] = [
        "/opt/homebrew/bin",   // Apple Silicon Homebrew
        "/usr/local/bin",      // Intel Homebrew
        "/usr/bin",
        "/bin"
    ]

    /// Returns the absolute path to the tool's executable, or nil if not installed.
    func resolveExecutablePath() -> String? {
        let fm = FileManager.default
        for dir in Self.searchPaths {
            let candidate = "\(dir)/\(executableName)"
            if fm.isExecutableFile(atPath: candidate) {
                return candidate
            }
        }
        return nil
    }
}
