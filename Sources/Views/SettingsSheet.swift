import SwiftUI
import AppKit

struct SettingsSheet: View {
    @Binding var settings: ConversionSettings
    let fullHistoryCount: Int
    let onClearFullHistory: () -> Void
    @Environment(\.dismiss) var dismiss

    @State private var showExperimentalAlert = false
    @State private var showClearFullHistoryAlert = false

    private let experimentalWarning = "By enabling this, you can turn ANY file into ANY OTHER file. this may cause unexpected results, or it wont even work at all. Don't say i didn't warn you."

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            // Header
            HStack {
                Text("Settings")
                    .font(.title2.weight(.semibold))
                Spacer()
                Button("Done") {
                    dismiss()
                }
                .keyboardShortcut(.defaultAction)
            }
            .padding(.horizontal, 20)
            .padding(.top, 18)
            .padding(.bottom, 12)

            Divider()

            Form {
                Section {
                    HStack {
                        Text("Quality")
                        Spacer()
                        Slider(value: Binding(
                            get: { Double(settings.imageQuality) },
                            set: { settings.imageQuality = Int($0) }
                        ), in: 1...100, step: 1)
                            .frame(width: 220)
                        Text("\(settings.imageQuality)")
                            .monospacedDigit()
                            .frame(width: 32, alignment: .trailing)
                            .foregroundStyle(.secondary)
                    }
                } header: {
                    Label("Image", systemImage: "photo")
                } footer: {
                    Text("Higher quality = larger files. 85 is a good default.")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }

                Section {
                    Picker("Bitrate", selection: $settings.audioBitrate) {
                        ForEach([96, 128, 192, 256, 320], id: \.self) { rate in
                            Text("\(rate) kbps").tag(rate)
                        }
                    }
                } header: {
                    Label("Audio", systemImage: "waveform")
                }

                Section {
                    HStack {
                        Text("Quality (CRF)")
                        Spacer()
                        Slider(value: Binding(
                            get: { Double(settings.videoCRF) },
                            set: { settings.videoCRF = Int($0) }
                        ), in: 18...30, step: 1)
                            .frame(width: 220)
                        Text("\(settings.videoCRF)")
                            .monospacedDigit()
                            .frame(width: 32, alignment: .trailing)
                            .foregroundStyle(.secondary)
                    }
                } header: {
                    Label("Video", systemImage: "film")
                } footer: {
                    Text("Lower CRF = better quality, larger file. 23 is a balanced default.")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }

                Section {
                    Picker("Save to", selection: $settings.outputFolderMode) {
                        ForEach(OutputFolderMode.allCases) { mode in
                            Text(mode.displayName).tag(mode)
                        }
                    }
                    if settings.outputFolderMode == .customFolder {
                        HStack {
                            Text(settings.customOutputFolderPath ?? "No folder chosen")
                                .foregroundStyle(settings.customOutputFolderPath == nil ? .secondary : .primary)
                                .lineLimit(1)
                                .truncationMode(.middle)
                            Spacer()
                            Button("Choose…") {
                                showFolderPanel()
                            }
                        }
                    }
                } header: {
                    Label("Output folder", systemImage: "folder")
                }

                Section {
                    Toggle("Convert into unsupported formats", isOn: $settings.weirdModeEnabled)
                        .onChange(of: settings.weirdModeEnabled) { newValue in
                            if newValue {
                                showExperimentalAlert = true
                            }
                        }
                } header: {
                    Label("Experimental", systemImage: "exclamationmark.triangle")
                }

                Section {
                    HStack {
                        Text("\(fullHistoryCount) conversion\(fullHistoryCount == 1 ? "" : "s") recorded")
                            .foregroundStyle(.secondary)
                        Spacer()
                        Button("Clear…") {
                            showClearFullHistoryAlert = true
                        }
                        .foregroundStyle(.red)
                    }
                } header: {
                    Label("Full History", systemImage: "clock.arrow.circlepath")
                } footer: {
                    Text("Stats use full history. Clearing it resets all conversion stats permanently.")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
            }
            .formStyle(.grouped)
        }
        .frame(width: 560, height: 600)
        .alert("Clear full history?", isPresented: $showClearFullHistoryAlert) {
            Button("Cancel", role: .cancel) {}
            Button("Clear", role: .destructive) {
                onClearFullHistory()
            }
        } message: {
            Text("This permanently deletes all \(fullHistoryCount) conversion record\(fullHistoryCount == 1 ? "" : "s") and resets your stats. Recent history in the main view is not affected.")
        }
        .alert("Enable experimental conversions?", isPresented: $showExperimentalAlert) {
            Button("Cancel", role: .cancel) {
                settings.weirdModeEnabled = false
            }
            Button("Enable Anyway", role: .destructive) {
                // Already true — leave it on.
            }
        } message: {
            Text(experimentalWarning)
        }
    }

    private func showFolderPanel() {
        let panel = NSOpenPanel()
        panel.allowsMultipleSelection = false
        panel.canChooseDirectories = true
        panel.canChooseFiles = false
        panel.prompt = "Choose"
        if panel.runModal() == .OK, let url = panel.url {
            settings.customOutputFolder = url
        }
    }
}
