import Foundation
import SwiftUI

@MainActor
final class AppViewModel: ObservableObject {
    // MARK: - Queue
    @Published var queue: [QueueItem] = []
    @Published var targetFormat: FileKind? {
        didSet {
            if oldValue != targetFormat {
                applyTargetToAll()
            }
        }
    }
    @Published var isConverting: Bool = false

    // MARK: - Settings (auto-persisted)
    @Published var settings: ConversionSettings {
        didSet {
            persistSettings()
            if oldValue.weirdModeEnabled != settings.weirdModeEnabled {
                reconcileTargetAfterWeirdToggle()
            }
        }
    }

    // MARK: - History (auto-persisted)

    /// Recent conversions shown in the main view. Capped at maxRecentHistory.
    /// Cleared by "Clear History" in the main view.
    @Published private(set) var history: [HistoryEntry] = []

    /// Full log used by the stats dashboard. Survives "Clear History".
    /// Only cleared explicitly from Settings.
    @Published private(set) var fullHistory: [HistoryEntry] = []

    private var conversionTask: Task<Void, Never>?
    private let maxRecentHistory = 25
    private let maxFullHistory = 10_000

    // MARK: - Init

    init() {
        // Load settings from UserDefaults
        if let data = UserDefaults.standard.data(forKey: Self.settingsKey),
           let loaded = try? JSONDecoder().decode(ConversionSettings.self, from: data) {
            self.settings = loaded
        } else {
            self.settings = ConversionSettings()
        }
        loadHistories()
    }

    // MARK: - Queue management

    func addFiles(_ urls: [URL]) {
        var added: [QueueItem] = []
        for url in urls {
            guard let kind = FormatDetector.detect(url: url) else { continue }
            // Skip if already queued with a non-finished status
            if queue.contains(where: { $0.url == url && !$0.status.isFinished }) { continue }
            let initialTarget: FileKind = {
                if let target = targetFormat,
                   ConversionRouter.canConvert(from: kind, to: target, settings: settings) {
                    return target
                }
                return ConversionRouter.validTargets(for: kind, settings: settings).first ?? kind
            }()
            added.append(QueueItem(url: url, sourceKind: kind, targetKind: initialTarget))
        }
        queue.append(contentsOf: added)

        // If no target chosen yet, pick a sensible default from the first item
        if targetFormat == nil, let first = queue.first {
            targetFormat = ConversionRouter.validTargets(for: first.sourceKind, settings: settings).first
            applyTargetToAll()
        }
    }

    func remove(_ item: QueueItem) {
        queue.removeAll { $0.id == item.id }
        if queue.isEmpty {
            targetFormat = nil
        }
    }

    func clearQueue() {
        queue.removeAll()
        targetFormat = nil
    }

    func clearFinished() {
        queue.removeAll { $0.status.isFinished }
        if queue.isEmpty { targetFormat = nil }
    }

    /// Apply current targetFormat to every queue item where the conversion is supported.
    /// Items where the target isn't supported keep their existing target.
    func applyTargetToAll() {
        guard let target = targetFormat else { return }
        for i in queue.indices where !queue[i].status.isFinished {
            if ConversionRouter.canConvert(from: queue[i].sourceKind, to: target, settings: settings) {
                queue[i].targetKind = target
            }
        }
    }

    /// Intersection of valid target formats across every active queue item.
    var validTargets: [FileKind] {
        let activeKinds = Set(queue.filter { !$0.status.isFinished }.map(\.sourceKind))
        guard !activeKinds.isEmpty else { return [] }

        var common: Set<FileKind>?
        for kind in activeKinds {
            let valid = Set(ConversionRouter.validTargets(for: kind, settings: settings))
            common = common?.intersection(valid) ?? valid
        }
        return Array(common ?? []).sorted { lhs, rhs in
            if lhs.category != rhs.category {
                return lhs.category.rawValue < rhs.category.rawValue
            }
            return lhs.displayName < rhs.displayName
        }
    }

    /// When the weird-mode toggle flips, the set of valid targets changes — re-pick a
    /// sensible default if the current one is no longer offered.
    private func reconcileTargetAfterWeirdToggle() {
        let valid = validTargets
        if let current = targetFormat, !valid.contains(current) {
            targetFormat = valid.first
        } else if targetFormat == nil, !queue.isEmpty {
            targetFormat = valid.first
        }
    }

    var hasPendingItems: Bool {
        queue.contains { $0.status.isPending }
    }

    var pendingItemCount: Int {
        queue.filter { $0.status.isPending }.count
    }

    // MARK: - Conversion

    func startConversion() {
        guard !isConverting else { return }
        conversionTask?.cancel()
        conversionTask = Task { @MainActor [weak self] in
            await self?.runConversion()
        }
    }

    func cancelConversion() {
        conversionTask?.cancel()
        conversionTask = nil
        isConverting = false
        // Reset any in-flight rows back to pending
        for i in queue.indices where queue[i].status.isConverting {
            queue[i].status = .pending
        }
    }

    /// Resets a failed item to pending and immediately starts (or continues) conversion.
    func retryItem(_ item: QueueItem) {
        guard let idx = queue.firstIndex(where: { $0.id == item.id }),
              queue[idx].status.isFailed else { return }
        queue[idx].status = .pending
        if !isConverting {
            startConversion()
        }
    }

    private func runConversion() async {
        isConverting = true
        defer {
            isConverting = false
            // Any row still marked converting was interrupted — reset it to pending.
            for i in queue.indices where queue[i].status.isConverting {
                queue[i].status = .pending
            }
        }

        applyTargetToAll()
        let pendingIDs = queue.filter { $0.status.isPending }.map(\.id)
        for id in pendingIDs {
            if Task.isCancelled { break }
            await convertItem(id: id)
        }
    }

    private func convertItem(id: UUID) async {
        if Task.isCancelled { return }
        guard let idx = queue.firstIndex(where: { $0.id == id }) else { return }
        queue[idx].status = .converting
        let item = queue[idx]
        let outputURL = computeOutputURL(for: item)

        guard let plan = ConversionRouter.plan(
            source: item.sourceKind,
            target: item.targetKind,
            inputURL: item.url,
            outputURL: outputURL,
            settings: settings
        ) else {
            finalize(id: id, item: item, output: nil,
                     message: "No conversion path from \(item.sourceKind.displayName) to \(item.targetKind.displayName).",
                     success: false)
            return
        }

        do {
            let result = try await ToolRunner.execute(plan)
            if Task.isCancelled { return }
            let success = result.success && FileManager.default.fileExists(atPath: outputURL.path)
            if success {
                finalize(id: id, item: item, output: outputURL, message: nil, success: true)
            } else {
                let raw = result.stderr.isEmpty ? result.stdout : result.stderr
                let msg = raw.trimmingCharacters(in: .whitespacesAndNewlines)
                let display = msg.isEmpty ? "Conversion failed (exit \(result.exitCode))." : msg
                finalize(id: id, item: item, output: nil, message: display, success: false)
            }
        } catch is CancellationError {
            // Cancelled — runConversion's defer will reset the row to pending.
            return
        } catch {
            if Task.isCancelled { return }
            finalize(id: id, item: item, output: nil, message: error.localizedDescription, success: false)
        }
    }

    private func finalize(id: UUID, item: QueueItem, output: URL?, message: String?, success: Bool) {
        if let idx = queue.firstIndex(where: { $0.id == id }) {
            if success, let output = output {
                queue[idx].status = .done(outputURL: output)
            } else {
                queue[idx].status = .failed(message: message ?? "Unknown error")
            }
        }
        let bytes = ((try? FileManager.default.attributesOfItem(atPath: item.url.path)) ?? [:])[FileAttributeKey.size] as? Int ?? 0
        let entry = HistoryEntry(
            sourceURL: item.url,
            sourceKind: item.sourceKind,
            outputURL: success ? output : nil,
            outputKind: item.targetKind,
            outcome: success ? .success : .failure,
            errorMessage: success ? nil : message,
            bytesProcessed: bytes
        )
        addHistoryEntry(entry)
    }

    var stats: ConversionStats { ConversionStats.compute(from: fullHistory) }

    private func computeOutputURL(for item: QueueItem) -> URL {
        let outputDir: URL
        switch settings.outputFolderMode {
        case .nextToSource:
            outputDir = item.url.deletingLastPathComponent()
        case .customFolder:
            outputDir = settings.customOutputFolder ?? item.url.deletingLastPathComponent()
        }
        let name = item.url.lastPathComponent
        // Strip compound extensions (e.g., .tar.gz) so the output is "file.zip" not "file.tar.zip".
        let baseName: String
        if name.lowercased().hasSuffix(".tar.gz") {
            baseName = String(name.dropLast(".tar.gz".count))
        } else {
            baseName = item.url.deletingPathExtension().lastPathComponent
        }
        return outputDir
            .appendingPathComponent(baseName)
            .appendingPathExtension(item.targetKind.canonicalExtension)
    }

    // MARK: - History

    /// Clears recent history shown in the main view. Full history (and stats) are unaffected.
    func clearHistory() {
        history = []
        saveRecentHistory()
    }

    /// Clears the full history log. This resets all stats.
    func clearFullHistory() {
        fullHistory = []
        saveFullHistory()
    }

    private func addHistoryEntry(_ entry: HistoryEntry) {
        history.insert(entry, at: 0)
        if history.count > maxRecentHistory {
            history = Array(history.prefix(maxRecentHistory))
        }
        saveRecentHistory()

        fullHistory.insert(entry, at: 0)
        if fullHistory.count > maxFullHistory {
            fullHistory = Array(fullHistory.prefix(maxFullHistory))
        }
        saveFullHistory()
    }

    private func loadHistories() {
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        if let data = try? Data(contentsOf: Self.recentHistoryFileURL),
           let loaded = try? decoder.decode([HistoryEntry].self, from: data) {
            history = loaded
        }
        if let data = try? Data(contentsOf: Self.fullHistoryFileURL),
           let loaded = try? decoder.decode([HistoryEntry].self, from: data) {
            fullHistory = loaded
        }
    }

    private func saveRecentHistory() {
        saveEntries(history, to: Self.recentHistoryFileURL)
    }

    private func saveFullHistory() {
        saveEntries(fullHistory, to: Self.fullHistoryFileURL)
    }

    private func saveEntries(_ entries: [HistoryEntry], to url: URL) {
        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .iso8601
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        guard let data = try? encoder.encode(entries) else { return }
        try? data.write(to: url, options: .atomic)
    }

    // MARK: - Persistence helpers

    private static let settingsKey = "ULTIMATE_FILE_CONVERTER_settings_v1"

    private static var appFolder: URL {
        let appSupport = FileManager.default
            .urls(for: .applicationSupportDirectory, in: .userDomainMask)
            .first!
        let folder = appSupport.appendingPathComponent("ULTIMATE-FILE-CONVERTER", isDirectory: true)
        try? FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        return folder
    }

    private static var recentHistoryFileURL: URL {
        appFolder.appendingPathComponent("history.json")
    }

    private static var fullHistoryFileURL: URL {
        appFolder.appendingPathComponent("history-full.json")
    }

    private func persistSettings() {
        let encoder = JSONEncoder()
        if let data = try? encoder.encode(settings) {
            UserDefaults.standard.set(data, forKey: Self.settingsKey)
        }
    }
}
