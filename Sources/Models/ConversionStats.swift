import Foundation

struct ConversionStats {
    struct FormatCount: Identifiable {
        var id: String { kind.rawValue }
        let kind: FileKind
        let count: Int
    }

    struct Pair: Identifiable {
        var id: String { "\(source.rawValue)-\(target.rawValue)" }
        let source: FileKind
        let target: FileKind
        let count: Int
    }

    let totalSuccesses: Int
    let totalFailures: Int
    let totalBytesProcessed: Int64
    let topSourceFormats: [FormatCount]
    let topPairs: [Pair]

    static func compute(from history: [HistoryEntry]) -> ConversionStats {
        let successes = history.filter { $0.outcome == .success }
        let failures = history.filter { $0.outcome == .failure }
        let totalBytes = successes.reduce(Int64(0)) { $0 + Int64($1.bytesProcessed) }

        var srcCounts: [FileKind: Int] = [:]
        var pairCounts: [String: (FileKind, FileKind, Int)] = [:]

        for entry in successes {
            srcCounts[entry.sourceKind, default: 0] += 1
            let key = "\(entry.sourceKind.rawValue)-\(entry.outputKind.rawValue)"
            if let existing = pairCounts[key] {
                pairCounts[key] = (existing.0, existing.1, existing.2 + 1)
            } else {
                pairCounts[key] = (entry.sourceKind, entry.outputKind, 1)
            }
        }

        let topSrc = srcCounts.sorted { $0.value > $1.value }.prefix(5)
            .map { FormatCount(kind: $0.key, count: $0.value) }
        let topP = pairCounts.values.sorted { $0.2 > $1.2 }.prefix(5)
            .map { Pair(source: $0.0, target: $0.1, count: $0.2) }

        return ConversionStats(
            totalSuccesses: successes.count,
            totalFailures: failures.count,
            totalBytesProcessed: totalBytes,
            topSourceFormats: topSrc,
            topPairs: topP
        )
    }

    var formattedBytes: String {
        let formatter = ByteCountFormatter()
        formatter.allowedUnits = [.useAll]
        formatter.countStyle = .file
        return formatter.string(fromByteCount: totalBytesProcessed)
    }
}
