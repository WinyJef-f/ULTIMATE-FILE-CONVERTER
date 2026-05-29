import SwiftUI
import AppKit

struct HistoryRowView: View {
    let entry: HistoryEntry
    let onReveal: (URL) -> Void

    var body: some View {
        HStack(spacing: 12) {
            Image(systemName: entry.sourceKind.category.sfSymbol)
                .font(.system(size: 13, weight: .medium))
                .foregroundStyle(.secondary)
                .frame(width: 26, height: 26)

            VStack(alignment: .leading, spacing: 2) {
                Text(entry.sourceName)
                    .font(.body)
                    .lineLimit(1)
                    .truncationMode(.middle)
                HStack(spacing: 5) {
                    Text(entry.sourceKind.displayName)
                    Image(systemName: "arrow.right")
                        .font(.system(size: 8, weight: .semibold))
                        .foregroundStyle(.tertiary)
                    Text(entry.outputKind.displayName)
                    Text("·").foregroundStyle(.tertiary)
                    Text(entry.relativeDate)
                }
                .font(.caption)
                .foregroundStyle(.secondary)
            }

            Spacer(minLength: 8)

            outcomeView
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 8)
    }

    @ViewBuilder
    private var outcomeView: some View {
        switch entry.outcome {
        case .success:
            if let url = entry.outputURL, FileManager.default.fileExists(atPath: url.path) {
                Button {
                    onReveal(url)
                } label: {
                    Image(systemName: "eye")
                        .font(.system(size: 14))
                }
                .buttonStyle(.borderless)
                .help("Reveal in Finder")
            } else {
                Image(systemName: "checkmark.circle")
                    .foregroundStyle(.secondary)
                    .help("Output file may have been moved or deleted")
            }
        case .failure:
            Image(systemName: "exclamationmark.triangle")
                .foregroundStyle(.red.opacity(0.7))
                .help(entry.errorMessage ?? "Conversion failed")
        }
    }
}
