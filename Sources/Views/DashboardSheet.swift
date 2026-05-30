import SwiftUI

struct DashboardSheet: View {
    let stats: ConversionStats
    @Environment(\.dismiss) var dismiss

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack {
                Text("Stats")
                    .font(.title2.weight(.semibold))
                Spacer()
                Button("Done") { dismiss() }
                    .keyboardShortcut(.defaultAction)
            }
            .padding(.horizontal, 20)
            .padding(.top, 18)
            .padding(.bottom, 12)

            Divider()

            ScrollView {
                VStack(alignment: .leading, spacing: 20) {
                    overviewCards

                    if !stats.topSourceFormats.isEmpty {
                        barSection(title: "Top formats",
                                   items: stats.topSourceFormats.map { ($0.kind.displayName, $0.count) })
                    }

                    if !stats.topPairs.isEmpty {
                        barSection(title: "Top conversions",
                                   items: stats.topPairs.map { ("\($0.source.displayName) → \($0.target.displayName)", $0.count) })
                    }

                    if stats.totalSuccesses == 0 && stats.totalFailures == 0 {
                        emptyState
                    }
                }
                .padding(20)
            }
        }
        .frame(width: 500, height: 500)
    }

    // MARK: - Subviews

    private var overviewCards: some View {
        HStack(spacing: 12) {
            statCard(title: "Converted", value: "\(stats.totalSuccesses)",
                     symbol: "checkmark.circle.fill", color: .green)
            statCard(title: "Failed", value: "\(stats.totalFailures)",
                     symbol: "xmark.circle.fill", color: .red)
            statCard(title: "Total size", value: stats.formattedBytes,
                     symbol: "doc.fill", color: .accentColor)
        }
    }

    private func statCard(title: String, value: String, symbol: String, color: Color) -> some View {
        VStack(alignment: .leading, spacing: 6) {
            Label(title, systemImage: symbol)
                .font(.caption)
                .foregroundStyle(color)
            Text(value)
                .font(.title2.weight(.semibold))
                .monospacedDigit()
                .lineLimit(1)
                .minimumScaleFactor(0.7)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(12)
        .background(
            RoundedRectangle(cornerRadius: 10)
                .fill(Color(nsColor: .controlBackgroundColor))
        )
        .overlay(
            RoundedRectangle(cornerRadius: 10)
                .strokeBorder(Color.secondary.opacity(0.12), lineWidth: 0.5)
        )
    }

    private func barSection(title: String, items: [(String, Int)]) -> some View {
        let maxCount = items.first?.1 ?? 1
        return VStack(alignment: .leading, spacing: 8) {
            Text(title).font(.headline)
            VStack(spacing: 0) {
                ForEach(Array(items.enumerated()), id: \.offset) { idx, item in
                    barRow(label: item.0, count: item.1, maxValue: maxCount)
                    if idx < items.count - 1 {
                        Divider().padding(.leading, 12)
                    }
                }
            }
            .background(
                RoundedRectangle(cornerRadius: 10)
                    .fill(Color(nsColor: .controlBackgroundColor))
            )
            .overlay(
                RoundedRectangle(cornerRadius: 10)
                    .strokeBorder(Color.secondary.opacity(0.12), lineWidth: 0.5)
            )
        }
    }

    private func barRow(label: String, count: Int, maxValue: Int) -> some View {
        let fraction = maxValue > 0 ? CGFloat(count) / CGFloat(maxValue) : 0
        return HStack(spacing: 10) {
            Text(label)
                .frame(maxWidth: .infinity, alignment: .leading)
                .lineLimit(1)
            ZStack(alignment: .leading) {
                RoundedRectangle(cornerRadius: 4)
                    .fill(Color.accentColor.opacity(0.15))
                    .frame(width: 110, height: 8)
                RoundedRectangle(cornerRadius: 4)
                    .fill(Color.accentColor)
                    .frame(width: max(4, 110 * fraction), height: 8)
            }
            Text("\(count)")
                .font(.caption.monospacedDigit())
                .foregroundStyle(.secondary)
                .frame(width: 28, alignment: .trailing)
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 9)
    }

    private var emptyState: some View {
        VStack(spacing: 8) {
            Image(systemName: "chart.bar")
                .font(.system(size: 36))
                .foregroundStyle(.secondary)
            Text("No conversions yet")
                .font(.headline)
            Text("Stats will appear here after you convert your first file.")
                .font(.subheadline)
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)
        }
        .frame(maxWidth: .infinity)
        .padding(.top, 60)
    }
}
