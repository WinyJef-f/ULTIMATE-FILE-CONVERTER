import SwiftUI
import AppKit

struct QueueRowView: View {
    let item: QueueItem
    var onRemove: () -> Void
    var onReveal: (URL) -> Void

    var body: some View {
        HStack(spacing: 12) {
            categoryBadge

            VStack(alignment: .leading, spacing: 3) {
                Text(item.displayName)
                    .font(.body.weight(.medium))
                    .lineLimit(1)
                    .truncationMode(.middle)
                metadataLine
                if case .failed(let msg) = item.status {
                    Text(msg)
                        .font(.caption2)
                        .foregroundStyle(.red)
                        .textSelection(.enabled)
                        .fixedSize(horizontal: false, vertical: true)
                        .padding(.top, 2)
                }
            }

            Spacer(minLength: 8)

            statusBadge

            actionsMenu
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 10)
        .background(
            RoundedRectangle(cornerRadius: 10)
                .fill(Color(nsColor: .controlBackgroundColor))
        )
        .overlay(
            RoundedRectangle(cornerRadius: 10)
                .strokeBorder(Color.secondary.opacity(0.15), lineWidth: 0.5)
        )
    }

    private var categoryBadge: some View {
        Image(systemName: item.sourceKind.category.sfSymbol)
            .font(.system(size: 16, weight: .medium))
            .foregroundStyle(.secondary)
            .frame(width: 34, height: 34)
            .background(
                Circle().fill(Color.secondary.opacity(0.12))
            )
    }

    private var metadataLine: some View {
        HStack(spacing: 6) {
            Text(item.sourceKind.displayName)
            Image(systemName: "arrow.right")
                .font(.system(size: 9, weight: .semibold))
                .foregroundStyle(.tertiary)
            Text(item.targetKind.displayName)
                .foregroundStyle(.primary)
                .fontWeight(.medium)
            if let size = item.formattedFileSize {
                Text("·").foregroundStyle(.tertiary)
                Text(size).foregroundStyle(.secondary)
            }
        }
        .font(.caption)
        .foregroundStyle(.secondary)
    }

    @ViewBuilder
    private var statusBadge: some View {
        switch item.status {
        case .pending:
            HStack(spacing: 4) {
                Image(systemName: "circle.dotted")
                Text("Pending")
            }
            .font(.caption)
            .foregroundStyle(.secondary)
        case .converting:
            HStack(spacing: 6) {
                ProgressView().controlSize(.mini)
                Text("Converting")
            }
            .font(.caption)
            .foregroundStyle(.secondary)
        case .done:
            HStack(spacing: 4) {
                Image(systemName: "checkmark.circle.fill")
                Text("Done")
            }
            .font(.caption.weight(.medium))
            .foregroundStyle(.green)
        case .failed:
            HStack(spacing: 4) {
                Image(systemName: "exclamationmark.triangle.fill")
                Text("Failed")
            }
            .font(.caption.weight(.medium))
            .foregroundStyle(.red)
        }
    }

    private var actionsMenu: some View {
        Menu {
            if case .done(let url) = item.status {
                Button {
                    onReveal(url)
                } label: {
                    Label("Reveal in Finder", systemImage: "magnifyingglass")
                }
                Button {
                    NSWorkspace.shared.open(url)
                } label: {
                    Label("Open", systemImage: "arrow.up.right.square")
                }
                Divider()
            }
            if case .failed(let msg) = item.status {
                Button {
                    NSPasteboard.general.clearContents()
                    NSPasteboard.general.setString(msg, forType: .string)
                } label: {
                    Label("Copy Error Message", systemImage: "doc.on.doc")
                }
                Divider()
            }
            Button(role: .destructive) {
                onRemove()
            } label: {
                Label("Remove from Queue", systemImage: "trash")
            }
        } label: {
            Image(systemName: "ellipsis")
                .font(.body)
                .foregroundStyle(.secondary)
                .frame(width: 28, height: 28)
                .contentShape(Rectangle())
        }
        .menuStyle(.borderlessButton)
        .menuIndicator(.hidden)
        .fixedSize()
    }
}
