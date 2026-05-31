import Foundation

// MARK: - Plan & Step

struct ConversionStep {
    let tool: Tool
    /// Arguments with placeholders: {INPUT}, {OUTPUT}, {OUTPUT_DIR}.
    let argumentTemplate: [String]
    let inputURL: URL
    let outputURL: URL

    func resolvedArguments() -> [String] {
        let outDir = outputURL.deletingLastPathComponent().path
        return argumentTemplate.map { token in
            token
                .replacingOccurrences(of: "{INPUT}", with: inputURL.path)
                .replacingOccurrences(of: "{OUTPUT}", with: outputURL.path)
                .replacingOccurrences(of: "{OUTPUT_DIR}", with: outDir)
        }
    }
}

struct ConversionPlan {
    let steps: [ConversionStep]

    init(steps: [ConversionStep]) {
        precondition(!steps.isEmpty, "ConversionPlan requires at least one step")
        self.steps = steps
    }

    /// Single-step convenience initializer.
    init(tool: Tool, arguments: [String], input: URL, output: URL) {
        self.init(steps: [
            ConversionStep(tool: tool, argumentTemplate: arguments,
                           inputURL: input, outputURL: output)
        ])
    }

    var inputURL: URL { steps.first!.inputURL }
    var outputURL: URL { steps.last!.outputURL }
}

// MARK: - Router

enum ConversionRouter {
    /// Builds a conversion plan for (source, target) using the provided settings.
    /// Returns nil for unsupported pairs (Phase 4 weird-mode will handle those).
    static func plan(source: FileKind,
                     target: FileKind,
                     inputURL: URL,
                     outputURL: URL,
                     settings: ConversionSettings = ConversionSettings()) -> ConversionPlan? {
        guard source != target else { return nil }

        let src = source.category
        let dst = target.category

        // --- SVG source: render natively via NSImage (handles WebKit-backed SVG) ---
        if source == .svg && dst == .image {
            return ConversionPlan(
                tool: .native,
                arguments: ["svg", "{INPUT}", "{OUTPUT}",
                            "\(settings.imageQuality)", target.rawValue],
                input: inputURL, output: outputURL
            )
        }

        // --- Image -> Image via macOS native CGImage frameworks (no external deps) ---
        if src == .image && dst == .image {
            return ConversionPlan(
                tool: .native,
                arguments: ["image", "{INPUT}", "{OUTPUT}",
                            "\(settings.imageQuality)", target.rawValue],
                input: inputURL, output: outputURL
            )
        }

        // --- Audio -> Audio via ffmpeg ---
        if src == .audio && dst == .audio {
            return ConversionPlan(
                tool: .ffmpeg,
                arguments: ["-y", "-i", "{INPUT}", "-b:a", "\(settings.audioBitrate)k", "{OUTPUT}"],
                input: inputURL, output: outputURL
            )
        }

        // --- Video -> Video via ffmpeg ---
        if src == .video && dst == .video {
            return ConversionPlan(
                tool: .ffmpeg,
                arguments: ["-y", "-i", "{INPUT}",
                            "-crf", "\(settings.videoCRF)",
                            "-preset", "medium",
                            "-c:a", "aac", "-b:a", "\(settings.audioBitrate)k",
                            "{OUTPUT}"],
                input: inputURL, output: outputURL
            )
        }

        // --- Video -> Audio: drop the video stream ---
        if src == .video && dst == .audio {
            return ConversionPlan(
                tool: .ffmpeg,
                arguments: ["-y", "-i", "{INPUT}", "-vn", "-b:a", "\(settings.audioBitrate)k", "{OUTPUT}"],
                input: inputURL, output: outputURL
            )
        }

        // --- Audio -> Video: pair audio with a black still image ---
        if src == .audio && dst == .video {
            return ConversionPlan(
                tool: .ffmpeg,
                arguments: [
                    "-y", "-f", "lavfi", "-i", "color=c=black:s=640x360",
                    "-i", "{INPUT}", "-shortest", "-pix_fmt", "yuv420p",
                    "-c:a", "aac", "-b:a", "\(settings.audioBitrate)k",
                    "{OUTPUT}"
                ],
                input: inputURL, output: outputURL
            )
        }

        // --- Image -> Video ---
        if src == .image && dst == .video {
            // Animated GIF: preserve frames natively
            if source == .gif {
                return ConversionPlan(
                    tool: .ffmpeg,
                    arguments: ["-y", "-i", "{INPUT}", "-pix_fmt", "yuv420p", "{OUTPUT}"],
                    input: inputURL, output: outputURL
                )
            }
            // Static image: loop for 5 seconds
            return ConversionPlan(
                tool: .ffmpeg,
                arguments: ["-y", "-loop", "1", "-i", "{INPUT}",
                            "-t", "5", "-pix_fmt", "yuv420p", "{OUTPUT}"],
                input: inputURL, output: outputURL
            )
        }

        // --- Video -> Image ---
        if src == .video && dst == .image {
            // Animated GIF target: preserve all frames
            if target == .gif {
                return ConversionPlan(
                    tool: .ffmpeg,
                    arguments: ["-y", "-i", "{INPUT}",
                                "-vf", "fps=12,scale=480:-1:flags=lanczos",
                                "-loop", "0", "{OUTPUT}"],
                    input: inputURL, output: outputURL
                )
            }
            // Single-frame extraction
            return ConversionPlan(
                tool: .ffmpeg,
                arguments: ["-y", "-i", "{INPUT}", "-vframes", "1", "{OUTPUT}"],
                input: inputURL, output: outputURL
            )
        }

        // --- PDF -> Image: rasterize first page via native CGPDFDocument ---
        if source == .pdf && dst == .image {
            return ConversionPlan(
                tool: .native,
                arguments: ["pdf", "{INPUT}", "{OUTPUT}",
                            "200", "\(settings.imageQuality)", target.rawValue],
                input: inputURL, output: outputURL
            )
        }

        // --- Document -> Document via pandoc ---
        if src == .document && dst == .document && isPandocPair(source: source, target: target) {
            return ConversionPlan(
                tool: .pandoc,
                arguments: ["{INPUT}", "-o", "{OUTPUT}"],
                input: inputURL, output: outputURL
            )
        }

        // --- Subtitle -> Subtitle ---
        // ffmpeg handles srt/ass/vtt directly. SBV has poor ffmpeg support, so it is
        // bridged through SRT in-process (the .native tool), then ffmpeg reaches ass/vtt.
        if src == .subtitle && dst == .subtitle {
            return subtitleRoute(source: source, target: target,
                                 inputURL: inputURL, outputURL: outputURL)
        }

        // --- Archive -> Archive via 7-Zip ---
        // All conversions go through a temp extract-then-recompress pipeline.
        // tar.gz targets need an extra step: create a .tar first, then gzip it.
        if src == .archive && dst == .archive {
            return archiveRoute(source: source, target: target,
                                inputURL: inputURL, outputURL: outputURL)
        }

        // --- LibreOffice (soffice) family conversions ---
        if let plan = sofficeRoute(source: source, target: target,
                                    inputURL: inputURL, outputURL: outputURL) {
            return plan
        }

        // --- Weird mode fallback: any → any with smart mappings + raw-byte interpretation ---
        if settings.weirdModeEnabled {
            return weirdRoute(source: source, target: target,
                              inputURL: inputURL, outputURL: outputURL,
                              settings: settings)
        }

        return nil
    }

    static func canConvert(from source: FileKind, to target: FileKind,
                           settings: ConversionSettings = ConversionSettings()) -> Bool {
        let probeInput = URL(fileURLWithPath: "/_in.\(source.canonicalExtension)")
        let probeOutput = URL(fileURLWithPath: "/_out.\(target.canonicalExtension)")
        return plan(source: source, target: target,
                    inputURL: probeInput, outputURL: probeOutput,
                    settings: settings) != nil
    }

    static func validTargets(for source: FileKind,
                              settings: ConversionSettings = ConversionSettings()) -> [FileKind] {
        FileKind.allCases.filter { $0 != source && canConvert(from: source, to: $0, settings: settings) }
    }

    private static func intermediateURL(for source: URL, extension ext: String) -> URL {
        let dir = FileManager.default.temporaryDirectory
            .appendingPathComponent("ULTIMATE-FILE-CONVERTER", isDirectory: true)
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        let name = UUID().uuidString + "." + ext
        return dir.appendingPathComponent(name)
    }

    /// Creates and returns a fresh temporary subdirectory for archive extraction.
    private static func intermediateDir() -> URL {
        let dir = FileManager.default.temporaryDirectory
            .appendingPathComponent("ULTIMATE-FILE-CONVERTER", isDirectory: true)
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        return dir
    }

    // MARK: - archive routing

    /// Extract-then-recompress pipeline via 7-Zip. All format pairs share the same extract
    /// step; tar.gz targets need a two-step compress (tar first, then gzip).
    private static func archiveRoute(source: FileKind, target: FileKind,
                                     inputURL: URL, outputURL: URL) -> ConversionPlan {
        let extractDir = intermediateDir()
        let extractPath = extractDir.path

        // Extract step: 7z x -y {INPUT} -o<extractDir>
        // The outputURL here is a dummy; the step's args don't use {OUTPUT}.
        let extractDummy = extractDir.appendingPathComponent(".done")
        let extractStep = ConversionStep(
            tool: .sevenZip,
            argumentTemplate: ["x", "-y", "{INPUT}", "-o\(extractPath)"],
            inputURL: inputURL,
            outputURL: extractDummy
        )

        // tar.gz target: 7z can't create .tar.gz in one pass — create .tar then gzip it.
        if target == .targz {
            let tarTemp = intermediateURL(for: outputURL, extension: "tar")
            return ConversionPlan(steps: [
                extractStep,
                ConversionStep(tool: .sevenZip,
                               argumentTemplate: ["a", "-ttar", "{OUTPUT}", "\(extractPath)/*", "-r"],
                               inputURL: extractDummy, outputURL: tarTemp),
                ConversionStep(tool: .sevenZip,
                               argumentTemplate: ["a", "-tgzip", "{OUTPUT}", "{INPUT}"],
                               inputURL: tarTemp, outputURL: outputURL)
            ])
        }

        let formatFlag = archiveFormatFlag(for: target)
        return ConversionPlan(steps: [
            extractStep,
            ConversionStep(tool: .sevenZip,
                           argumentTemplate: ["a", formatFlag, "{OUTPUT}", "\(extractPath)/*", "-r"],
                           inputURL: extractDummy, outputURL: outputURL)
        ])
    }

    private static func archiveFormatFlag(for kind: FileKind) -> String {
        switch kind {
        case .zip: return "-tzip"
        case .sevenz: return "-t7z"
        case .tar: return "-ttar"
        default: return "-tzip"
        }
    }

    // MARK: - subtitle routing

    /// Builds a subtitle→subtitle plan. srt/ass/vtt go straight through ffmpeg; SBV is
    /// translated to/from SRT in-process first (ffmpeg's SBV support is unreliable).
    private static func subtitleRoute(source: FileKind, target: FileKind,
                                      inputURL: URL, outputURL: URL) -> ConversionPlan {
        // SBV source: SBV → SRT (native), then SRT → target via ffmpeg if needed.
        if source == .sbv {
            if target == .srt {
                return ConversionPlan(
                    tool: .native,
                    arguments: ["sbv2srt", "{INPUT}", "{OUTPUT}"],
                    input: inputURL, output: outputURL
                )
            }
            let srt = intermediateURL(for: inputURL, extension: "srt")
            return ConversionPlan(steps: [
                ConversionStep(tool: .native,
                               argumentTemplate: ["sbv2srt", "{INPUT}", "{OUTPUT}"],
                               inputURL: inputURL, outputURL: srt),
                ConversionStep(tool: .ffmpeg,
                               argumentTemplate: ["-y", "-i", "{INPUT}", "{OUTPUT}"],
                               inputURL: srt, outputURL: outputURL)
            ])
        }

        // SBV target: source → SRT via ffmpeg if needed, then SRT → SBV (native).
        if target == .sbv {
            if source == .srt {
                return ConversionPlan(
                    tool: .native,
                    arguments: ["srt2sbv", "{INPUT}", "{OUTPUT}"],
                    input: inputURL, output: outputURL
                )
            }
            let srt = intermediateURL(for: inputURL, extension: "srt")
            return ConversionPlan(steps: [
                ConversionStep(tool: .ffmpeg,
                               argumentTemplate: ["-y", "-i", "{INPUT}", "{OUTPUT}"],
                               inputURL: inputURL, outputURL: srt),
                ConversionStep(tool: .native,
                               argumentTemplate: ["srt2sbv", "{INPUT}", "{OUTPUT}"],
                               inputURL: srt, outputURL: outputURL)
            ])
        }

        // srt ⇄ ass ⇄ vtt: direct ffmpeg.
        return ConversionPlan(
            tool: .ffmpeg,
            arguments: ["-y", "-i", "{INPUT}", "{OUTPUT}"],
            input: inputURL, output: outputURL
        )
    }

    // MARK: - soffice family routing

    private static let sofficeTextFamily: Set<FileKind> = [.docx, .doc, .odt, .rtf, .html, .txt]
    private static let sofficeSheetFamily: Set<FileKind> = [.xlsx, .ods, .csv]
    private static let sofficePresFamily: Set<FileKind> = [.pptx, .odp]
    private static var sofficeReadable: Set<FileKind> {
        sofficeTextFamily.union(sofficeSheetFamily).union(sofficePresFamily)
    }

    private static func sofficeRoute(source: FileKind, target: FileKind,
                                     inputURL: URL, outputURL: URL) -> ConversionPlan? {
        if target == .pdf && sofficeReadable.contains(source) {
            return sofficePlan(target: target, inputURL: inputURL, outputURL: outputURL)
        }
        if sofficeTextFamily.contains(source) && sofficeTextFamily.contains(target) {
            return sofficePlan(target: target, inputURL: inputURL, outputURL: outputURL)
        }
        if sofficeSheetFamily.contains(source) && sofficeSheetFamily.contains(target) {
            return sofficePlan(target: target, inputURL: inputURL, outputURL: outputURL)
        }
        if sofficePresFamily.contains(source) && sofficePresFamily.contains(target) {
            return sofficePlan(target: target, inputURL: inputURL, outputURL: outputURL)
        }
        return nil
    }

    private static func sofficePlan(target: FileKind, inputURL: URL, outputURL: URL) -> ConversionPlan {
        // Use an isolated profile directory so our bundled soffice doesn't fight
        // a user's separately-installed LibreOffice (if any) over the default profile.
        let profileDir = NSTemporaryDirectory().appending("ULTIMATE-FILE-CONVERTER-soffice/")
        return ConversionPlan(
            tool: .soffice,
            arguments: [
                "-env:UserInstallation=file://\(profileDir)",
                "--headless",
                "--convert-to", target.canonicalExtension,
                "--outdir", "{OUTPUT_DIR}",
                "{INPUT}"
            ],
            input: inputURL, output: outputURL
        )
    }

    /// Pandoc-supported document pairs (best-effort; native readers/writers).
    /// tex is allowed for text-to-text conversions but not for PDF output
    /// (which would need a TeX engine we don't bundle).
    private static func isPandocPair(source: FileKind, target: FileKind) -> Bool {
        let pandocKinds: Set<FileKind> = [.md, .html, .docx, .odt, .rtf, .epub, .txt, .tex]
        return pandocKinds.contains(source) && pandocKinds.contains(target)
    }

    // MARK: - Experimental mode (raw-byte fallback)

    /// Any source → any target. Pure raw-byte reinterpretation: the source file's
    /// bytes are read as if they were the target format. For document/sheet/presentation
    /// targets we just copy the file with a renamed extension. Results vary wildly;
    /// the warning text in Settings is the contract with the user.
    private static func weirdRoute(source: FileKind, target: FileKind,
                                    inputURL: URL, outputURL: URL,
                                    settings: ConversionSettings) -> ConversionPlan? {
        switch target.category {
        case .image:
            return rawBytesToImage(target: target, inputURL: inputURL,
                                   outputURL: outputURL, settings: settings)
        case .audio:
            return rawBytesToAudio(target: target, inputURL: inputURL, outputURL: outputURL)
        case .video:
            return rawBytesToVideo(target: target, inputURL: inputURL, outputURL: outputURL)
        case .document, .spreadsheet, .presentation, .subtitle, .archive:
            // No "raw decode" makes sense for these — just copy bytes with the new extension.
            return ConversionPlan(
                tool: .cp,
                arguments: ["{INPUT}", "{OUTPUT}"],
                input: inputURL, output: outputURL
            )
        }
    }

    private static func rawBytesToImage(target: FileKind, inputURL: URL, outputURL: URL,
                                         settings: ConversionSettings) -> ConversionPlan {
        // 256x256 grayscale = 64 KiB / frame, fits inside most small files.
        let ffmpegArgs = ["-y", "-f", "rawvideo", "-pixel_format", "gray",
                          "-video_size", "256x256", "-i", "{INPUT}",
                          "-frames:v", "1", "{OUTPUT}"]
        if target == .png {
            return ConversionPlan(tool: .ffmpeg, arguments: ffmpegArgs,
                                  input: inputURL, output: outputURL)
        }
        // ffmpeg writes PNG, native CGImage transcodes to the final target format.
        let intermediate = intermediateURL(for: inputURL, extension: "png")
        return ConversionPlan(steps: [
            ConversionStep(tool: .ffmpeg, argumentTemplate: ffmpegArgs,
                           inputURL: inputURL, outputURL: intermediate),
            ConversionStep(tool: .native,
                           argumentTemplate: ["image", "{INPUT}", "{OUTPUT}",
                                              "\(settings.imageQuality)", target.rawValue],
                           inputURL: intermediate, outputURL: outputURL)
        ])
    }

    private static func rawBytesToAudio(target: FileKind, inputURL: URL, outputURL: URL) -> ConversionPlan {
        // Interpret the source's bytes as 8-bit unsigned mono PCM at 22 kHz.
        ConversionPlan(
            tool: .ffmpeg,
            arguments: ["-y", "-f", "u8", "-ar", "22050", "-ac", "1",
                        "-i", "{INPUT}", "{OUTPUT}"],
            input: inputURL, output: outputURL
        )
    }

    private static func rawBytesToVideo(target: FileKind, inputURL: URL, outputURL: URL) -> ConversionPlan {
        // Interpret the source's bytes as raw RGB frames in a 256x256 grid at 10 fps.
        ConversionPlan(
            tool: .ffmpeg,
            arguments: ["-y", "-f", "rawvideo", "-pixel_format", "rgb24",
                        "-video_size", "256x256", "-framerate", "10",
                        "-i", "{INPUT}", "-pix_fmt", "yuv420p", "{OUTPUT}"],
            input: inputURL, output: outputURL
        )
    }
}
