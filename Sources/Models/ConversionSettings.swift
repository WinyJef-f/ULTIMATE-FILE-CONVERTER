import Foundation

enum OutputFolderMode: String, Codable, CaseIterable, Identifiable {
    case nextToSource
    case customFolder

    var id: String { rawValue }

    var displayName: String {
        switch self {
        case .nextToSource: return "Next to source file"
        case .customFolder: return "Custom folder…"
        }
    }
}

struct ConversionSettings: Codable, Equatable {
    /// Image quality 1-100. Applied via `-quality` to ImageMagick.
    var imageQuality: Int = 85

    /// Audio bitrate in kbps. Applied via `-b:a Nk` to ffmpeg.
    var audioBitrate: Int = 192

    /// Video constant rate factor (lower = better, larger files). Applied via `-crf` to ffmpeg.
    var videoCRF: Int = 23

    /// Where converted files are written.
    var outputFolderMode: OutputFolderMode = .nextToSource

    /// Absolute path of the custom output folder (when outputFolderMode == .customFolder).
    var customOutputFolderPath: String?

    /// When true, the router will route ANY source -> ANY target using smart mappings
    /// (audio→spectrogram, text→speech) and raw-byte fallbacks. Results vary wildly.
    var weirdModeEnabled: Bool = false

    var customOutputFolder: URL? {
        get { customOutputFolderPath.map { URL(fileURLWithPath: $0) } }
        set { customOutputFolderPath = newValue?.path }
    }
}
