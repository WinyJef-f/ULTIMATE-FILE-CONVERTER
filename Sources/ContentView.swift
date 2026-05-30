import SwiftUI
import AppKit

struct ContentView: View {
    @EnvironmentObject var vm: AppViewModel
    @Binding var showSettings: Bool
    @State private var showDashboard = false

    var body: some View {
        Group {
            if vm.queue.isEmpty && vm.history.isEmpty {
                emptyState
            } else {
                contentState
            }
        }
        .frame(minWidth: 640, idealWidth: 760, minHeight: 520, idealHeight: 680)
        .toolbar { toolbarContent }
        .sheet(isPresented: $showSettings) {
            SettingsSheet(settings: $vm.settings,
                          fullHistoryCount: vm.fullHistory.count,
                          onClearFullHistory: { vm.clearFullHistory() })
        }
        .sheet(isPresented: $showDashboard) {
            DashboardSheet(stats: vm.stats)
        }
    }

    // MARK: - Empty state (no queue, no history)

    private var emptyState: some View {
        VStack {
            DropZoneView(isCompact: false) { urls in
                vm.addFiles(urls)
            }
        }
        .padding(20)
    }

    // MARK: - Content state (queue or history present)

    private var contentState: some View {
        ScrollView {
            VStack(spacing: 16) {
                DropZoneView(isCompact: true) { urls in
                    vm.addFiles(urls)
                }

                if !vm.queue.isEmpty {
                    queueSection
                }

                if !vm.history.isEmpty {
                    historySection
                }
            }
            .padding(20)
        }
    }

    // MARK: - Queue section

    private var queueSection: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(spacing: 10) {
                Text("Queue")
                    .font(.title3.weight(.semibold))
                Text("\(vm.queue.count) file\(vm.queue.count == 1 ? "" : "s")")
                    .font(.subheadline)
                    .foregroundStyle(.secondary)

                Spacer(minLength: 8)

                TargetPickerBar(target: $vm.targetFormat, options: vm.validTargets)

                if vm.queue.contains(where: { $0.status.isFinished }) {
                    Button("Clear Finished") {
                        withAnimation(.spring(response: 0.4, dampingFraction: 0.85)) {
                            vm.clearFinished()
                        }
                    }
                    .buttonStyle(.borderless)
                    .foregroundStyle(.secondary)
                }
            }

            VStack(spacing: 6) {
                ForEach(vm.queue) { item in
                    QueueRowView(
                        item: item,
                        onRemove: {
                            withAnimation(.spring(response: 0.4, dampingFraction: 0.85)) {
                                vm.remove(item)
                            }
                        },
                        onReveal: { url in
                            NSWorkspace.shared.activateFileViewerSelecting([url])
                        }
                    )
                    .transition(.asymmetric(
                        insertion: .move(edge: .top).combined(with: .opacity),
                        removal: .opacity.combined(with: .scale(scale: 0.96))
                    ))
                }
            }
        }
    }

    // MARK: - History section

    private var historySection: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(spacing: 10) {
                Text("Recent")
                    .font(.title3.weight(.semibold))
                Text("\(vm.history.count)")
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
                Spacer()
                Button("Clear History") {
                    vm.clearHistory()
                }
                .buttonStyle(.borderless)
                .foregroundStyle(.secondary)
            }

            VStack(spacing: 0) {
                let visible = Array(vm.history.prefix(25))
                ForEach(visible) { entry in
                    HistoryRowView(entry: entry) { url in
                        NSWorkspace.shared.activateFileViewerSelecting([url])
                    }
                    if entry.id != visible.last?.id {
                        Divider().padding(.leading, 50)
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

    // MARK: - Toolbar

    @ToolbarContentBuilder
    private var toolbarContent: some ToolbarContent {
        ToolbarItemGroup(placement: .navigation) {
            Button {
                addFilesViaPanel()
            } label: {
                Label("Add Files", systemImage: "plus")
            }
            .help("Add files to the queue (⌘O)")
            .keyboardShortcut("o", modifiers: .command)

            Button {
                withAnimation(.spring(response: 0.4, dampingFraction: 0.85)) {
                    vm.clearQueue()
                }
            } label: {
                Label("Clear Queue", systemImage: "xmark.circle")
            }
            .help("Remove all files from the queue")
            .disabled(vm.queue.isEmpty || vm.isConverting)
        }

        ToolbarItem(placement: .automatic) {
            Button {
                showDashboard = true
            } label: {
                Label("Stats", systemImage: "chart.bar")
            }
            .help("Conversion stats")
        }

        ToolbarItem(placement: .automatic) {
            Button {
                showSettings = true
            } label: {
                Label("Settings", systemImage: "gearshape")
            }
            .help("Conversion settings (⌘,)")
        }

        ToolbarItem(placement: .primaryAction) {
            if vm.isConverting {
                Button {
                    vm.cancelConversion()
                } label: {
                    Label("Cancel", systemImage: "stop.fill")
                }
                .help("Cancel the running conversion")
            } else {
                Button {
                    vm.startConversion()
                } label: {
                    Label(convertButtonTitle, systemImage: "wand.and.stars")
                }
                .help("Convert all pending files (⌘R)")
                .keyboardShortcut("r", modifiers: .command)
                .disabled(!vm.hasPendingItems || vm.targetFormat == nil)
            }
        }
    }

    private var convertButtonTitle: String {
        let n = vm.pendingItemCount
        if n == 0 { return "Convert" }
        return "Convert \(n) file\(n == 1 ? "" : "s")"
    }

    // MARK: - Helpers

    private func addFilesViaPanel() {
        let panel = NSOpenPanel()
        panel.allowsMultipleSelection = true
        panel.canChooseDirectories = false
        panel.canChooseFiles = true
        if panel.runModal() == .OK {
            vm.addFiles(panel.urls)
        }
    }
}

#Preview {
    ContentView(showSettings: .constant(false))
        .environmentObject(AppViewModel())
}
