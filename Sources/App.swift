import SwiftUI
import AppKit

// Handles the NSApplication delegate events that SwiftUI doesn't expose — specifically
// application(_:open:) for files sent via "Open With" and the right-click context menu.
class AppDelegate: NSObject, NSApplicationDelegate {
    weak var viewModel: AppViewModel?

    func application(_ application: NSApplication, open urls: [URL]) {
        viewModel?.addFiles(urls)
    }
}

@main
struct UltimateFileConverterApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) var appDelegate
    @StateObject private var vm = AppViewModel()
    @State private var showSettings = false
    @State private var showSetup = false
    @State private var showUpdateAlert = false
    @State private var updateTagName = ""
    @State private var updateReleaseURL: URL? = nil

    var body: some Scene {
        WindowGroup("ULTIMATE-FILE-CONVERTER") {
            ContentView(showSettings: $showSettings, showSetup: $showSetup)
                .environmentObject(vm)
                .task { await startup() }
                .sheet(isPresented: $showSetup) {
                    SetupSheet { showSetup = false }
                }
                .alert("Update Available", isPresented: $showUpdateAlert) {
                    Button("OK", role: .cancel) {}
                    Button("Take Me There") {
                        if let url = updateReleaseURL {
                            NSWorkspace.shared.open(url)
                        }
                    }
                } message: {
                    Text("Version \(updateTagName) is available on GitHub.")
                }
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

    private func startup() async {
        // Wire the delegate so files opened via "Open With" / context menu reach the queue.
        appDelegate.viewModel = vm
        // First-run: show Homebrew setup sheet if any tools are missing.
        if BrewDependencyService.shouldOfferFirstRunSetup {
            showSetup = true
        }
        // Silently check for updates; show alert only if a newer release exists.
        guard let result = try? await UpdateChecker.check() else { return }
        if case .available(let tagName, let releaseURL) = result {
            updateTagName = tagName
            updateReleaseURL = releaseURL
            showUpdateAlert = true
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
