import Foundation

// MARK: - Entry point

let _exitSemaphore = DispatchSemaphore(value: 0)
var _exitCode: Int32 = 0

Task {
    _exitCode = await runCLI()
    _exitSemaphore.signal()
}
_exitSemaphore.wait()
exit(_exitCode)

// MARK: - CLI logic

func runCLI() async -> Int32 {
    let args = Array(CommandLine.arguments.dropFirst())

    guard let command = args.first else {
        printHelp()
        return 2
    }

    switch command {
    case "version":
        print("ULTIMATE-FILE-CONVERTER ufc 1.2.0")
        return 0
    case "list-targets":
        return listTargets(args: Array(args.dropFirst()))
    case "convert":
        return await convert(args: Array(args.dropFirst()))
    case "--help", "-h", "help":
        printHelp()
        return 0
    default:
        fputs("ufc: unknown command '\(command)'\n", stderr)
        printHelp()
        return 2
    }
}

func printHelp() {
    print("""
    Usage: ufc <command> [options]

    Commands:
      convert <input> <output> [--quality N]
          Convert a file. Exit 0 on success, 1 on failure, 2 on bad arguments.
      list-targets <input>
          Print valid conversion targets for a file.
      version
          Print version.
    """)
}

func listTargets(args: [String]) -> Int32 {
    guard let inputPath = args.first else {
        fputs("ufc list-targets: missing input file\n", stderr)
        return 2
    }

    let inputURL = URL(fileURLWithPath: (inputPath as NSString).expandingTildeInPath)
    guard let kind = FormatDetector.detect(url: inputURL) else {
        fputs("ufc: unrecognized format: \(inputPath)\n", stderr)
        return 1
    }

    let targets = ConversionRouter.validTargets(for: kind)
    if targets.isEmpty {
        print("No conversion targets for \(kind.displayName).")
    } else {
        print("Targets for \(kind.displayName):")
        for target in targets {
            print("  \(target.canonicalExtension)  (\(target.displayName))")
        }
    }
    return 0
}

func convert(args: [String]) async -> Int32 {
    var positional: [String] = []
    var quality = ConversionSettings().imageQuality

    var i = 0
    while i < args.count {
        switch args[i] {
        case "--quality":
            i += 1
            guard i < args.count, let q = Int(args[i]), q >= 1, q <= 100 else {
                fputs("ufc: --quality requires an integer between 1 and 100\n", stderr)
                return 2
            }
            quality = q
        default:
            positional.append(args[i])
        }
        i += 1
    }

    guard positional.count >= 2 else {
        fputs("ufc convert: requires <input> <output>\n", stderr)
        return 2
    }

    let rawIn  = (positional[0] as NSString).expandingTildeInPath
    let rawOut = (positional[1] as NSString).expandingTildeInPath
    let inputURL  = URL(fileURLWithPath: rawIn)
    let outputURL = URL(fileURLWithPath: rawOut)

    guard FileManager.default.fileExists(atPath: rawIn) else {
        fputs("ufc: file not found: \(rawIn)\n", stderr)
        return 1
    }

    guard let sourceKind = FormatDetector.detect(url: inputURL) else {
        fputs("ufc: unrecognized source format: \(rawIn)\n", stderr)
        return 1
    }

    let outputExt = outputURL.pathExtension.lowercased()
    guard let targetKind = FileKind.allCases.first(where: {
        $0.canonicalExtension == outputExt || $0.recognizedExtensions.contains(outputExt)
    }), !targetKind.isSourceOnly else {
        fputs("ufc: unrecognized or unsupported target format: \(outputExt)\n", stderr)
        return 1
    }

    var settings = ConversionSettings()
    settings.imageQuality = quality

    guard let plan = ConversionRouter.plan(
        source: sourceKind, target: targetKind,
        inputURL: inputURL, outputURL: outputURL,
        settings: settings
    ) else {
        fputs("ufc: no conversion path from \(sourceKind.displayName) to \(targetKind.displayName)\n", stderr)
        return 1
    }

    do {
        let result = try await ToolRunner.execute(plan)
        if result.success && FileManager.default.fileExists(atPath: rawOut) {
            print("✓ \(rawOut)")
            return 0
        } else {
            let raw = result.stderr.isEmpty ? result.stdout : result.stderr
            let msg = raw.trimmingCharacters(in: .whitespacesAndNewlines)
            fputs("ufc: conversion failed\(msg.isEmpty ? "" : ": \(msg)")\n", stderr)
            return 1
        }
    } catch {
        fputs("ufc: \(error.localizedDescription)\n", stderr)
        return 1
    }
}
