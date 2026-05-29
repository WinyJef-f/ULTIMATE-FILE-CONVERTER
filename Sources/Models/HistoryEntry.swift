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

    init(id: UUID = UUID(),
         date: Date = Date(),
         sourceURL: URL,
         sourceKind: FileKind,
         outputURL: URL?,
         outputKind: FileKind,
         outcome: Outcome,
         errorMessage: String? = nil) {
        self.id = id
        self.date = date
        self.sourceURL = sourceURL
        self.sourceKind = sourceKind
        self.outputURL = outputURL
        self.outputKind = outputKind
        self.outcome = outcome
        self.errorMessage = errorMessage
    }

    var sourceName: String { sourceURL.lastPathComponent }
    var outputName: String? { outputURL?.lastPathComponent }

    var relativeDate: String {
        let formatter = RelativeDateTimeFormatter()
        formatter.unitsStyle = .abbreviated
        return formatter.localizedString(for: date, relativeTo: Date())
    }
}
