import Foundation

struct ProcessResult {
    let exitCode: Int32
    let stdout: String
    let stderr: String

    var success: Bool { exitCode == 0 }
}

enum ToolRunnerError: LocalizedError {
    case toolNotFound(Tool)
    case launchFailed(String)

    var errorDescription: String? {
        switch self {
        case .toolNotFound(let t):
            return "\(t.executableName) is not installed. Try: brew install \(t.brewFormula)"
        case .launchFailed(let msg):
            return "Failed to launch tool: \(msg)"
        }
    }
}

extension Tool {
    /// Homebrew formula name (sometimes differs from executable name).
    var brewFormula: String {
        switch self {
        case .ffmpeg: return "ffmpeg"
        case .pandoc: return "pandoc"
        case .soffice: return "--cask libreoffice"
        case .sevenZip: return "p7zip"
        case .cp: return "" // /bin/cp is built into macOS
        case .native: return "" // not an external tool
        }
    }
}

enum ToolRunner {
    /// Executes a multi-step conversion plan. Throws CancellationError if the surrounding
    /// Task is cancelled. Stops on the first failed step.
    static func execute(_ plan: ConversionPlan) async throws -> ProcessResult {
        var lastResult = ProcessResult(exitCode: 0, stdout: "", stderr: "")
        for step in plan.steps {
            try Task.checkCancellation()

            // Native steps are executed in-process by NativeConverter, not as a subprocess.
            if step.tool == .native {
                lastResult = try await NativeConverter.run(arguments: step.resolvedArguments())
                try Task.checkCancellation()
                continue
            }

            guard let executable = step.tool.resolveExecutablePath() else {
                throw ToolRunnerError.toolNotFound(step.tool)
            }
            lastResult = try await run(executable: executable, arguments: step.resolvedArguments())
            if !lastResult.success {
                return lastResult
            }
            try Task.checkCancellation()
        }
        return lastResult
    }

    static func run(executable: String, arguments: [String]) async throws -> ProcessResult {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: executable)
        process.arguments = arguments

        let stdoutPipe = Pipe()
        let stderrPipe = Pipe()
        process.standardOutput = stdoutPipe
        process.standardError = stderrPipe

        return try await withTaskCancellationHandler {
            try await withCheckedThrowingContinuation { (cont: CheckedContinuation<ProcessResult, Error>) in
                process.terminationHandler = { proc in
                    let outData = (try? stdoutPipe.fileHandleForReading.readToEnd()) ?? Data()
                    let errData = (try? stderrPipe.fileHandleForReading.readToEnd()) ?? Data()
                    let stdout = String(data: outData, encoding: .utf8) ?? ""
                    let stderr = String(data: errData, encoding: .utf8) ?? ""
                    cont.resume(returning: ProcessResult(
                        exitCode: proc.terminationStatus,
                        stdout: stdout,
                        stderr: stderr
                    ))
                }

                do {
                    try process.run()
                } catch {
                    cont.resume(throwing: ToolRunnerError.launchFailed(error.localizedDescription))
                }
            }
        } onCancel: {
            // SIGTERM the child process so ffmpeg / magick / etc. die promptly.
            if process.isRunning {
                process.terminate()
            }
        }
    }
}
