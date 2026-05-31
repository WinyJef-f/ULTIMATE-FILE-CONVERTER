import SwiftUI
import Foundation

struct SetupSheet: View {
    let onDone: () -> Void
    @Environment(\.dismiss) private var dismiss

    @State private var logLines: [String] = []
    @State private var isRunning = false
    @State private var isDone = false
    @State private var task: Task<Void, Never>?

    private var brewPath: String? { BrewDependencyService.findBrew() }
    private var canInstall: Bool { brewPath != nil && !BrewDependencyService.missing.isEmpty }

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {

            // MARK: Header
            HStack {
                VStack(alignment: .leading, spacing: 2) {
                    Text("Conversion Tools")
                        .font(.title2.weight(.semibold))
                    Text("Manage conversion tools installed via Homebrew")
                        .font(.subheadline)
                        .foregroundStyle(.secondary)
                }
                Spacer()
            }
            .padding(.horizontal, 20)
            .padding(.top, 18)
            .padding(.bottom, 12)

            Divider()

            // MARK: Body
            VStack(alignment: .leading, spacing: 16) {

                // Dependency list
                VStack(spacing: 0) {
                    ForEach(BrewDependencyService.required) { dep in
                        HStack(alignment: .top, spacing: 10) {
                            Image(systemName: dep.isInstalled ? "checkmark.circle.fill" : "circle")
                                .foregroundStyle(dep.isInstalled ? Color.green : Color(nsColor: .tertiaryLabelColor))
                                .font(.system(size: 16))
                                .frame(width: 20, height: 20)
                                .padding(.top, 1)

                            VStack(alignment: .leading, spacing: 1) {
                                Text(dep.friendlyName)
                                    .fontWeight(.semibold)
                                Text(dep.purpose)
                                    .font(.caption)
                                    .foregroundStyle(.secondary)
                            }

                            Spacer()
                        }
                        .padding(.vertical, 7)
                        .padding(.horizontal, 4)

                        if dep.id != BrewDependencyService.required.last?.id {
                            Divider()
                                .padding(.leading, 34)
                        }
                    }
                }
                .padding(.horizontal, 4)
                .background(
                    RoundedRectangle(cornerRadius: 8, style: .continuous)
                        .fill(Color(nsColor: .controlBackgroundColor))
                )
                .overlay(
                    RoundedRectangle(cornerRadius: 8, style: .continuous)
                        .strokeBorder(Color(nsColor: .separatorColor), lineWidth: 0.5)
                )

                // Homebrew not found warning
                if brewPath == nil {
                    Label("Homebrew not found — install from brew.sh", systemImage: "exclamationmark.triangle.fill")
                        .font(.caption)
                        .foregroundStyle(.orange)
                }

                // Installation log
                ScrollViewReader { proxy in
                    ScrollView(.vertical) {
                        VStack(alignment: .leading, spacing: 2) {
                            if logLines.isEmpty {
                                Text("Install log will appear here…")
                                    .font(.system(.caption, design: .monospaced))
                                    .foregroundStyle(Color(nsColor: .tertiaryLabelColor))
                            } else {
                                ForEach(Array(logLines.enumerated()), id: \.offset) { index, line in
                                    Text(line)
                                        .font(.system(.caption, design: .monospaced))
                                        .foregroundStyle(logLineColor(line))
                                        .frame(maxWidth: .infinity, alignment: .leading)
                                        .id(index)
                                }
                            }
                        }
                        .padding(8)
                        .frame(maxWidth: .infinity, alignment: .leading)
                    }
                    .frame(height: 150)
                    .background(Color(nsColor: .textBackgroundColor))
                    .clipShape(RoundedRectangle(cornerRadius: 6, style: .continuous))
                    .overlay(
                        RoundedRectangle(cornerRadius: 6, style: .continuous)
                            .strokeBorder(Color(nsColor: .separatorColor), lineWidth: 0.5)
                    )
                    .onChange(of: logLines.count) { _ in
                        if let last = logLines.indices.last {
                            withAnimation(.easeOut(duration: 0.15)) {
                                proxy.scrollTo(last, anchor: .bottom)
                            }
                        }
                    }
                }
            }
            .padding(20)

            Divider()

            // MARK: Footer
            HStack {
                Button("Skip") {
                    task?.cancel()
                    dismiss()
                }
                .disabled(isRunning)

                Spacer()

                if isRunning {
                    HStack(spacing: 8) {
                        ProgressView()
                            .controlSize(.small)
                        Text("Installing…")
                            .foregroundStyle(.secondary)
                    }
                } else if isDone {
                    Button("Done") {
                        onDone()
                        dismiss()
                    }
                    .keyboardShortcut(.defaultAction)
                } else {
                    Button("Install Missing Tools") {
                        startInstall()
                    }
                    .keyboardShortcut(.defaultAction)
                    .disabled(!canInstall)
                }
            }
            .padding(.horizontal, 20)
            .padding(.vertical, 14)
        }
        .frame(width: 480)
    }

    // MARK: Actions

    private func startInstall() {
        guard !isRunning else { return }
        isRunning = true
        logLines = []

        task = Task {
            let ok = await BrewDependencyService.installMissingAsync { line in
                Task { @MainActor in
                    logLines.append(line)
                }
            }
            await MainActor.run {
                isRunning = false
                isDone = true
                if ok {
                    // Trigger a SwiftUI refresh so checkmarks update.
                    logLines.append("")
                    logLines.removeLast()
                }
            }
        }
    }

    // MARK: Helpers

    private func logLineColor(_ line: String) -> Color {
        if line.hasPrefix("✓") { return .green }
        if line.hasPrefix("✗") { return .red }
        return Color(nsColor: .labelColor)
    }
}

// MARK: - Preview

#Preview {
    SetupSheet(onDone: {})
}
