import Foundation

/// In-process subtitle text conversion between SubRip (SRT) and YouTube SBV.
///
/// FFmpeg handles SRT ⇄ ASS ⇄ VTT directly, but its SBV support is unreliable, so the
/// router bridges SBV through SRT using this converter (the `.native` tool on macOS):
/// SBV → SRT → {ass,vtt} and {ass,vtt} → SRT → SBV. The two formats differ only in their
/// cue framing and timestamp punctuation, so the bridge is a faithful, lossless round-trip
/// of timings and text.
///
/// SRT cue:
/// ```
/// 1
/// 00:00:01,000 --> 00:00:04,000
/// First line
/// Second line
/// ```
/// SBV cue:
/// ```
/// 0:00:01.000,0:00:04.000
/// First line
/// Second line
/// ```
enum SubtitleConverter {
    enum SubtitleError: LocalizedError {
        case readFailed(String)
        case writeFailed(String)

        var errorDescription: String? {
            switch self {
            case .readFailed(let m):  return "Failed to read subtitle: \(m)"
            case .writeFailed(let m): return "Failed to write subtitle: \(m)"
            }
        }
    }

    /// One subtitle cue: a time range (in milliseconds) and its text lines.
    private struct Cue {
        var startMs: Int
        var endMs: Int
        var lines: [String]
    }

    // MARK: - Public entry points (dispatched from NativeConverter)

    static func sbvToSRT(input: URL, output: URL) throws {
        let text = try read(input)
        let cues = parseSBV(text)
        try write(buildSRT(cues), to: output)
    }

    static func srtToSBV(input: URL, output: URL) throws {
        let text = try read(input)
        let cues = parseSRT(text)
        try write(buildSBV(cues), to: output)
    }

    // MARK: - Parsing

    /// Splits text into cue blocks separated by one or more blank lines.
    private static func blocks(_ text: String) -> [[String]] {
        let normalized = text
            .replacingOccurrences(of: "\r\n", with: "\n")
            .replacingOccurrences(of: "\r", with: "\n")
        var result: [[String]] = []
        var current: [String] = []
        for rawLine in normalized.components(separatedBy: "\n") {
            if rawLine.trimmingCharacters(in: .whitespaces).isEmpty {
                if !current.isEmpty { result.append(current); current = [] }
            } else {
                current.append(rawLine)
            }
        }
        if !current.isEmpty { result.append(current) }
        return result
    }

    private static func parseSRT(_ text: String) -> [Cue] {
        var cues: [Cue] = []
        for block in blocks(text) {
            guard let timingIndex = block.firstIndex(where: { $0.contains("-->") }) else { continue }
            let timing = block[timingIndex]
            let parts = timing.components(separatedBy: "-->")
            guard parts.count == 2,
                  let start = parseTimestamp(parts[0]),
                  let end = parseTimestamp(parts[1]) else { continue }
            let lines = Array(block[(timingIndex + 1)...])
            cues.append(Cue(startMs: start, endMs: end, lines: lines))
        }
        return cues
    }

    private static func parseSBV(_ text: String) -> [Cue] {
        var cues: [Cue] = []
        for block in blocks(text) {
            guard let first = block.first else { continue }
            let parts = first.components(separatedBy: ",")
            guard parts.count == 2,
                  let start = parseTimestamp(parts[0]),
                  let end = parseTimestamp(parts[1]) else { continue }
            let lines = Array(block.dropFirst())
            cues.append(Cue(startMs: start, endMs: end, lines: lines))
        }
        return cues
    }

    /// Parses `H:MM:SS.mmm` or `HH:MM:SS,mmm` (either punctuation) into milliseconds.
    private static func parseTimestamp(_ raw: String) -> Int? {
        let trimmed = raw.trimmingCharacters(in: .whitespaces).replacingOccurrences(of: ",", with: ".")
        let timeParts = trimmed.components(separatedBy: ":")
        guard timeParts.count == 3 else { return nil }
        let secondsParts = timeParts[2].components(separatedBy: ".")
        guard let hours = Int(timeParts[0]),
              let minutes = Int(timeParts[1]),
              let seconds = Int(secondsParts[0]) else { return nil }
        let millis = secondsParts.count > 1 ? Int(secondsParts[1].padded(to: 3)) ?? 0 : 0
        return ((hours * 60 + minutes) * 60 + seconds) * 1000 + millis
    }

    // MARK: - Building

    private static func buildSRT(_ cues: [Cue]) -> String {
        var out = ""
        for (index, cue) in cues.enumerated() {
            out += "\(index + 1)\n"
            out += "\(formatSRT(cue.startMs)) --> \(formatSRT(cue.endMs))\n"
            out += cue.lines.joined(separator: "\n")
            out += "\n\n"
        }
        return out
    }

    private static func buildSBV(_ cues: [Cue]) -> String {
        var out = ""
        for cue in cues {
            out += "\(formatSBV(cue.startMs)),\(formatSBV(cue.endMs))\n"
            out += cue.lines.joined(separator: "\n")
            out += "\n\n"
        }
        return out
    }

    /// `HH:MM:SS,mmm` (SubRip).
    private static func formatSRT(_ ms: Int) -> String {
        let (h, m, s, millis) = split(ms)
        return String(format: "%02d:%02d:%02d,%03d", h, m, s, millis)
    }

    /// `H:MM:SS.mmm` (YouTube SBV — hours are not zero-padded).
    private static func formatSBV(_ ms: Int) -> String {
        let (h, m, s, millis) = split(ms)
        return String(format: "%d:%02d:%02d.%03d", h, m, s, millis)
    }

    private static func split(_ ms: Int) -> (Int, Int, Int, Int) {
        let clamped = max(0, ms)
        return (clamped / 3_600_000, (clamped / 60_000) % 60, (clamped / 1000) % 60, clamped % 1000)
    }

    // MARK: - IO

    private static func read(_ url: URL) throws -> String {
        do {
            return try String(contentsOf: url, encoding: .utf8)
        } catch {
            // Fall back to lossy decoding so an odd-encoding file still converts.
            guard let data = try? Data(contentsOf: url) else {
                throw SubtitleError.readFailed(url.lastPathComponent)
            }
            return String(decoding: data, as: UTF8.self)
        }
    }

    private static func write(_ text: String, to url: URL) throws {
        do {
            try text.write(to: url, atomically: true, encoding: .utf8)
        } catch {
            throw SubtitleError.writeFailed(error.localizedDescription)
        }
    }
}

private extension String {
    /// Right-pads a fractional-seconds string with zeros so e.g. "5" → "500" (centiseconds → ms).
    func padded(to length: Int) -> String {
        if count >= length { return String(prefix(length)) }
        return self + String(repeating: "0", count: length - count)
    }
}
