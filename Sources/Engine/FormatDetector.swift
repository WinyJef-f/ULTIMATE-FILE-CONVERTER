import Foundation
import UniformTypeIdentifiers

enum FormatDetector {
    /// Detects the file kind, first by extension (fast & usually right) and
    /// falling back to UTType content sniffing for extension-less or mislabeled files.
    static func detect(url: URL) -> FileKind? {
        if let byExt = detectByExtension(url: url) {
            return byExt
        }
        return detectByContent(url: url)
    }

    static func detectByExtension(url: URL) -> FileKind? {
        let ext = url.pathExtension.lowercased()
        guard !ext.isEmpty else { return nil }
        return FileKind.allCases.first { $0.recognizedExtensions.contains(ext) }
    }

    /// Use macOS's UniformTypeIdentifiers to sniff the file's actual content type,
    /// then map back to FileKind via the type's known filename extensions.
    static func detectByContent(url: URL) -> FileKind? {
        guard let values = try? url.resourceValues(forKeys: [.contentTypeKey]),
              let type = values.contentType else {
            return nil
        }
        let extensions = type.tags[.filenameExtension] ?? []
        for ext in extensions {
            let lower = ext.lowercased()
            if let kind = FileKind.allCases.first(where: { $0.recognizedExtensions.contains(lower) }) {
                return kind
            }
        }
        return nil
    }
}
