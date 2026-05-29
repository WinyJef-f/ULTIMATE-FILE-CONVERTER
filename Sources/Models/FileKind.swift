import Foundation

enum FileCategory: String, CaseIterable, Hashable {
    case image, audio, video, document, spreadsheet, presentation

    var displayName: String {
        switch self {
        case .image: return "Image"
        case .audio: return "Audio"
        case .video: return "Video"
        case .document: return "Document"
        case .spreadsheet: return "Spreadsheet"
        case .presentation: return "Presentation"
        }
    }

    var sfSymbol: String {
        switch self {
        case .image: return "photo"
        case .audio: return "waveform"
        case .video: return "film"
        case .document: return "doc.text"
        case .spreadsheet: return "tablecells"
        case .presentation: return "rectangle.on.rectangle"
        }
    }
}

enum FileKind: String, CaseIterable, Codable, Identifiable, Hashable {
    // Image
    case jpeg, png, webp, heic, avif, gif, bmp, tiff, svg, ico
    // Audio
    case mp3, wav, flac, aac, m4a, ogg, opus, aiff
    // Video
    case mp4, mov, mkv, webm, avi
    // Document
    case pdf, docx, doc, odt, rtf, html, md, epub, txt, tex
    // Spreadsheet
    case xlsx, ods, csv
    // Presentation
    case pptx, odp

    var id: String { rawValue }

    var category: FileCategory {
        switch self {
        case .jpeg, .png, .webp, .heic, .avif, .gif, .bmp, .tiff, .svg, .ico: return .image
        case .mp3, .wav, .flac, .aac, .m4a, .ogg, .opus, .aiff: return .audio
        case .mp4, .mov, .mkv, .webm, .avi: return .video
        case .pdf, .docx, .doc, .odt, .rtf, .html, .md, .epub, .txt, .tex: return .document
        case .xlsx, .ods, .csv: return .spreadsheet
        case .pptx, .odp: return .presentation
        }
    }

    var displayName: String {
        switch self {
        case .jpeg: return "JPEG"
        case .png: return "PNG"
        case .webp: return "WebP"
        case .heic: return "HEIC"
        case .gif: return "GIF"
        case .bmp: return "BMP"
        case .tiff: return "TIFF"
        case .avif: return "AVIF"
        case .svg: return "SVG"
        case .ico: return "ICO"
        case .mp3: return "MP3"
        case .wav: return "WAV"
        case .flac: return "FLAC"
        case .aac: return "AAC"
        case .m4a: return "M4A"
        case .ogg: return "OGG"
        case .opus: return "Opus"
        case .aiff: return "AIFF"
        case .mp4: return "MP4"
        case .mov: return "MOV"
        case .mkv: return "MKV"
        case .webm: return "WebM"
        case .avi: return "AVI"
        case .pdf: return "PDF"
        case .docx: return "Word (.docx)"
        case .doc: return "Word (.doc)"
        case .odt: return "OpenDocument Text"
        case .rtf: return "Rich Text"
        case .html: return "HTML"
        case .md: return "Markdown"
        case .epub: return "EPUB"
        case .txt: return "Plain Text"
        case .tex: return "LaTeX"
        case .xlsx: return "Excel (.xlsx)"
        case .ods: return "OpenDocument Sheet"
        case .csv: return "CSV"
        case .pptx: return "PowerPoint (.pptx)"
        case .odp: return "OpenDocument Pres."
        }
    }

    /// Every file extension (lowercased, no dot) that maps to this kind.
    var recognizedExtensions: [String] {
        switch self {
        case .jpeg: return ["jpg", "jpeg", "jpe", "jfif"]
        case .png: return ["png"]
        case .webp: return ["webp"]
        case .heic: return ["heic", "heif"]
        case .gif: return ["gif"]
        case .bmp: return ["bmp"]
        case .tiff: return ["tif", "tiff"]
        case .avif: return ["avif"]
        case .svg: return ["svg"]
        case .ico: return ["ico"]
        case .mp3: return ["mp3"]
        case .wav: return ["wav", "wave"]
        case .flac: return ["flac"]
        case .aac: return ["aac"]
        case .m4a: return ["m4a"]
        case .ogg: return ["ogg", "oga"]
        case .opus: return ["opus"]
        case .aiff: return ["aiff", "aif", "aifc"]
        case .mp4: return ["mp4", "m4v"]
        case .mov: return ["mov"]
        case .mkv: return ["mkv"]
        case .webm: return ["webm"]
        case .avi: return ["avi"]
        case .pdf: return ["pdf"]
        case .docx: return ["docx"]
        case .doc: return ["doc"]
        case .odt: return ["odt"]
        case .rtf: return ["rtf"]
        case .html: return ["html", "htm"]
        case .md: return ["md", "markdown", "mdown"]
        case .epub: return ["epub"]
        case .txt: return ["txt", "text"]
        case .tex: return ["tex", "latex"]
        case .xlsx: return ["xlsx"]
        case .ods: return ["ods"]
        case .csv: return ["csv"]
        case .pptx: return ["pptx"]
        case .odp: return ["odp"]
        }
    }

    /// Extension used when writing an output file.
    var canonicalExtension: String {
        recognizedExtensions.first ?? rawValue
    }
}
