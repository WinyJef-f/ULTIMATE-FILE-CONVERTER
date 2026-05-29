import Foundation

enum QueueItemStatus: Equatable {
    case pending
    case converting
    case done(outputURL: URL)
    case failed(message: String)

    var isPending: Bool { if case .pending = self { return true } else { return false } }
    var isConverting: Bool { if case .converting = self { return true } else { return false } }
    var isDone: Bool { if case .done = self { return true } else { return false } }
    var isFailed: Bool { if case .failed = self { return true } else { return false } }
    var isFinished: Bool { isDone || isFailed }
}

struct QueueItem: Identifiable, Equatable {
    let id: UUID
    let url: URL
    let sourceKind: FileKind
    var targetKind: FileKind
    var status: QueueItemStatus

    init(id: UUID = UUID(),
         url: URL,
         sourceKind: FileKind,
         targetKind: FileKind,
         status: QueueItemStatus = .pending) {
        self.id = id
        self.url = url
        self.sourceKind = sourceKind
        self.targetKind = targetKind
        self.status = status
    }

    var displayName: String { url.lastPathComponent }

    var fileSizeBytes: Int64? {
        guard let size = try? url.resourceValues(forKeys: [.fileSizeKey]).fileSize else {
            return nil
        }
        return Int64(size)
    }

    var formattedFileSize: String? {
        guard let bytes = fileSizeBytes else { return nil }
        return ByteCountFormatter.string(fromByteCount: bytes, countStyle: .file)
    }
}
