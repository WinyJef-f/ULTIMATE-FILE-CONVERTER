import SwiftUI
import AppKit

// Handles the NSApplication delegate events that SwiftUI doesn't expose — specifically
// application(_:open:) for files sent via "Open With" and the right-click context menu.
class AppDelegate: NSObject, NSApplicationDelegate {
    weak var viewModel: AppViewModel?

    func application(_ application: NSApplication, open urls: [URL]) {
        viewModel?.addFiles(urls)
    }

    // Called by macOS when the user selects Services → "Convert with UFC" in Finder.
    @objc func convertWithUFC(_ pasteboard: NSPasteboard, userData: String, error: AutoreleasingUnsafeMutablePointer<NSString?>) {
        // Finder sends NSFilenamesPboardType; other callers may send file URLs.
        var urls: [URL] = []
        if let paths = pasteboard.propertyList(forType: NSPasteboard.PasteboardType("NSFilenamesPboardType")) as? [String] {
            urls = paths.map { URL(fileURLWithPath: $0) }
        } else if let fileURLs = pasteboard.readObjects(forClasses: [NSURL.self]) as? [URL] {
            urls = fileURLs
        }
        guard !urls.isEmpty else { return }
        let vm = viewModel
        Task { @MainActor in
            NSApp.activate(ignoringOtherApps: true)
            vm?.addFiles(urls)
        }
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
        // Register the delegate as the NSServices provider so convertWithUFC(_:userData:error:) is called.
        NSApp.servicesProvider = appDelegate
        // Ensure the "Convert with UFC" Finder service is enabled. macOS adds NSServices entries
        // in a disabled state by default; users would otherwise have to find them in System Settings.
        enableConvertService()
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

    private func enableConvertService() {
        let domain = "com.apple.ServicesMenu.Services" as CFString
        let key = "Convert with UFC" as CFString
        // Only write if the user hasn't already configured this service (don't override a deliberate disable).
        guard CFPreferencesCopyValue(key, domain, kCFPreferencesCurrentUser, kCFPreferencesAnyHost) == nil else { return }
        CFPreferencesSetValue(key, kCFBooleanTrue, domain, kCFPreferencesCurrentUser, kCFPreferencesAnyHost)
        CFPreferencesSynchronize(domain, kCFPreferencesCurrentUser, kCFPreferencesAnyHost)
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
