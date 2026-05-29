import SwiftUI
import AppKit

@main
struct UltimateFileConverterApp: App {
    @StateObject private var vm = AppViewModel()
    @State private var showSettings = false

    var body: some Scene {
        WindowGroup("ULTIMATE-FILE-CONVERTER") {
            ContentView(showSettings: $showSettings)
                .environmentObject(vm)
        }
        .windowResizability(.contentMinSize)
        .commands {
            // Replace the default "Settings…" item so it opens our sheet.
            CommandGroup(replacing: .appSettings) {
                Button("Settings…") {
                    showSettings = true
                }
                .keyboardShortcut(",", modifiers: .command)
            }

            CommandGroup(replacing: .newItem) {
                Button("Add Files…") {
                    addFilesViaPanel()
                }
                .keyboardShortcut("o", modifiers: .command)
            }

            CommandGroup(after: .newItem) {
                Divider()
                Button(vm.isConverting ? "Cancel Conversion" : "Convert") {
                    if vm.isConverting {
                        vm.cancelConversion()
                    } else {
                        vm.startConversion()
                    }
                }
                .keyboardShortcut("r", modifiers: .command)
                .disabled(!vm.isConverting && (!vm.hasPendingItems || vm.targetFormat == nil))
            }
        }
    }

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
