import SwiftUI
import UniformTypeIdentifiers
import AppKit

struct DropZoneView: View {
    let isCompact: Bool
    let onPick: ([URL]) -> Void

    @State private var isTargeted = false

    var body: some View {
        let cornerRadius: CGFloat = 12
        Group {
            if isCompact { compactBody } else { expandedBody }
        }
        .background(
            RoundedRectangle(cornerRadius: cornerRadius)
                .fill(isTargeted ? Color.accentColor.opacity(0.12) : Color(nsColor: .controlBackgroundColor))
        )
        .overlay(
            RoundedRectangle(cornerRadius: cornerRadius)
                .strokeBorder(
                    isTargeted ? Color.accentColor : Color.secondary.opacity(0.3),
                    style: StrokeStyle(lineWidth: 2, dash: [6])
                )
        )
        .contentShape(RoundedRectangle(cornerRadius: cornerRadius))
        .onTapGesture { showOpenPanel() }
        .onDrop(of: [UTType.fileURL], isTargeted: $isTargeted) { providers in
            handleDrop(providers: providers)
        }
    }

    private var expandedBody: some View {
        VStack(spacing: 14) {
            Image(systemName: "square.and.arrow.down.on.square")
                .font(.system(size: 56, weight: .light))
                .foregroundStyle(.tertiary)
            Text("Drop files here")
                .font(.title2.weight(.semibold))
            Text("Or click to browse — multiple files supported")
                .foregroundStyle(.secondary)
                .font(.callout)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .padding(40)
    }

    private var compactBody: some View {
        HStack(spacing: 12) {
            Image(systemName: "plus.circle.dashed")
                .font(.title3)
                .foregroundStyle(isTargeted ? Color.accentColor : .secondary)
            Text(isTargeted ? "Release to add files" : "Drop more files here, or click to add")
                .foregroundStyle(.secondary)
            Spacer()
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 12)
    }

    private func handleDrop(providers: [NSItemProvider]) -> Bool {
        Task { @MainActor in
            var urls: [URL] = []
            for provider in providers {
                if let url = await Self.loadURL(from: provider) {
                    urls.append(url)
                }
            }
            if !urls.isEmpty {
                onPick(urls)
            }
        }
        return true
    }

    private static func loadURL(from provider: NSItemProvider) async -> URL? {
        await withCheckedContinuation { (cont: CheckedContinuation<URL?, Never>) in
            provider.loadItem(forTypeIdentifier: UTType.fileURL.identifier, options: nil) { item, _ in
                let url: URL?
                if let data = item as? Data {
                    url = URL(dataRepresentation: data, relativeTo: nil)
                } else if let direct = item as? URL {
                    url = direct
                } else {
                    url = nil
                }
                cont.resume(returning: url)
            }
        }
    }

    private func showOpenPanel() {
        let panel = NSOpenPanel()
        panel.allowsMultipleSelection = true
        panel.canChooseDirectories = false
        panel.canChooseFiles = true
        if panel.runModal() == .OK {
            onPick(panel.urls)
        }
    }
}
