import Foundation

/// Checks GitHub Releases for a version newer than the current build.
enum UpdateChecker {
    static let currentVersion = "1.1.0"

    private static let apiURL = URL(string: "https://api.github.com/repos/WinyJef-f/ULTIMATE-FILE-CONVERTER/releases/latest")!

    enum CheckResult {
        case upToDate
        case available(tagName: String, releaseURL: URL)
    }

    static func check() async throws -> CheckResult {
        var request = URLRequest(url: apiURL, cachePolicy: .reloadIgnoringLocalCacheData, timeoutInterval: 10)
        request.setValue("application/vnd.github+json", forHTTPHeaderField: "Accept")

        let (data, _) = try await URLSession.shared.data(for: request)
        guard let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              let tagName = json["tag_name"] as? String,
              let htmlString = json["html_url"] as? String,
              let releaseURL = URL(string: htmlString) else {
            throw URLError(.cannotParseResponse)
        }

        return isNewer(tagName, than: currentVersion)
            ? .available(tagName: tagName, releaseURL: releaseURL)
            : .upToDate
    }

    static func isNewer(_ tag: String, than current: String) -> Bool {
        let clean = tag.hasPrefix("v") ? String(tag.dropFirst()) : tag
        return compareVersions(clean, current) > 0
    }

    private static func compareVersions(_ a: String, _ b: String) -> Int {
        let pa = a.split(separator: ".").compactMap { Int($0) }
        let pb = b.split(separator: ".").compactMap { Int($0) }
        for i in 0..<max(pa.count, pb.count) {
            let va = i < pa.count ? pa[i] : 0
            let vb = i < pb.count ? pb[i] : 0
            if va != vb { return va > vb ? 1 : -1 }
        }
        return 0
    }
}
