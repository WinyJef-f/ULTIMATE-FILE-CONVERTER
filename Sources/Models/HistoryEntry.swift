import Foundation

struct HistoryEntry: Identifiable, Codable, Equatable {
    enum Outcome: String, Codable {
        case success
        case failure
    }

    let id: UUID
    let date: Date
    let sourceURL: URL
    let sourceKind: FileKind
    let outputURL: URL?
    let outputKind: FileKind
    let outcome: Outcome
    let errorMessage: String?
    /// Source file size in bytes captured at conversion time. 0 for entries predating this field.
    let bytesProcessed: Int

    init(id: UUID = UUID(),
         date: Date = Date(),
         sourceURL: URL,
         sourceKind: FileKind,
         outputURL: URL?,
         outputKind: FileKind,
         outcome: Outcome,
         errorMessage: String? = nil,
         bytesProcessed: Int = 0) {
        self.id = id
        self.date = date
        self.sourceURL = sourceURL
        self.sourceKind = sourceKind
        self.outputURL = outputURL
        self.outputKind = outputKind
        self.outcome = outcome
        self.errorMessage = errorMessage
        self.bytesProcessed = bytesProcessed
    }

    // Custom decoder so existing history files (without bytesProcessed) load as 0.
    enum CodingKeys: String, CodingKey {
        case id, date, sourceURL, sourceKind, outputURL, outputKind, outcome, errorMessage, bytesProcessed
    }

    init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        id = try c.decode(UUID.self, forKey: .id)
        date = try c.decode(Date.self, forKey: .date)
        sourceURL = try c.decode(URL.self, forKey: .sourceURL)
        sourceKind = try c.decode(FileKind.self, forKey: .sourceKind)
        outputURL = try c.decodeIfPresent(URL.self, forKey: .outputURL)
        outputKind = try c.decode(FileKind.self, forKey: .outputKind)
        outcome = try c.decode(Outcome.self, forKey: .outcome)
        errorMessage = try c.decodeIfPresent(String.self, forKey: .errorMessage)
        bytesProcessed = (try? c.decodeIfPresent(Int.self, forKey: .bytesProcessed)) ?? 0
    }

    var sourceName: String { sourceURL.lastPathComponent }
    var outputName: String? { outputURL?.lastPathComponent }

    var relativeDate: String {
        let formatter = RelativeDateTimeFormatter()
        formatter.unitsStyle = .abbreviated
        return formatter.localizedString(for: date, relativeTo: Date())
    }
}
