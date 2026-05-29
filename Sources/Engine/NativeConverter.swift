import Foundation
import AppKit
import CoreGraphics
import ImageIO
import UniformTypeIdentifiers

/// In-process raster image conversions backed by macOS native frameworks.
/// Handles every image format CGImageDestination can write, plus SVG via NSImage
/// and PDF rasterization via CGPDFDocument. Replaces external dependence on
/// ImageMagick / librsvg / Ghostscript for the common image paths.
enum NativeConverter {
    enum NativeError: LocalizedError {
        case unsupportedTargetFormat(FileKind)
        case readFailed(String)
        case writeFailed(String)
        case invalidStep(String)

        var errorDescription: String? {
            switch self {
            case .unsupportedTargetFormat(let k):
                return "This macOS version can't natively write \(k.displayName) files."
            case .readFailed(let m):  return "Failed to read source: \(m)"
            case .writeFailed(let m): return "Failed to write output: \(m)"
            case .invalidStep(let m): return "Invalid native step: \(m)"
            }
        }
    }

    // MARK: - Dispatch

    /// Argument template encoding:
    ///   ["image", "{INPUT}", "{OUTPUT}", quality, targetKindRaw]
    ///   ["svg",   "{INPUT}", "{OUTPUT}", quality, targetKindRaw]
    ///   ["pdf",   "{INPUT}", "{OUTPUT}", dpi, quality, targetKindRaw]
    /// Returns a synthetic ProcessResult so ToolRunner's caller doesn't have to special-case.
    static func run(arguments: [String]) async throws -> ProcessResult {
        guard let action = arguments.first else {
            throw NativeError.invalidStep("empty argument list")
        }
        guard arguments.count >= 3 else {
            throw NativeError.invalidStep("too few arguments for \(action)")
        }
        let input = URL(fileURLWithPath: arguments[1])
        let output = URL(fileURLWithPath: arguments[2])

        switch action {
        case "image":
            let quality = parseInt(arguments, at: 3, default: 85)
            let target = parseKind(arguments, at: 4)
            try convertImage(source: input, target: output, targetKind: target, quality: quality)
        case "svg":
            let quality = parseInt(arguments, at: 3, default: 85)
            let target = parseKind(arguments, at: 4)
            try convertSVG(source: input, target: output, targetKind: target, quality: quality)
        case "pdf":
            let dpi = parseInt(arguments, at: 3, default: 200)
            let quality = parseInt(arguments, at: 4, default: 85)
            let target = parseKind(arguments, at: 5)
            try rasterizePDF(source: input, target: output, targetKind: target, dpi: dpi, quality: quality)
        default:
            throw NativeError.invalidStep("unknown action: \(action)")
        }

        return ProcessResult(exitCode: 0, stdout: "", stderr: "")
    }

    private static func parseInt(_ args: [String], at index: Int, default value: Int) -> Int {
        guard index < args.count, let n = Int(args[index]) else { return value }
        return n
    }

    private static func parseKind(_ args: [String], at index: Int) -> FileKind {
        guard index < args.count, let kind = FileKind(rawValue: args[index]) else { return .png }
        return kind
    }

    // MARK: - Conversions

    static func convertImage(source: URL, target: URL, targetKind: FileKind, quality: Int) throws {
        guard let imgSource = CGImageSourceCreateWithURL(source as CFURL, nil),
              let cgImage = CGImageSourceCreateImageAtIndex(imgSource, 0, nil) else {
            throw NativeError.readFailed("Could not decode \(source.lastPathComponent)")
        }
        try write(cgImage: cgImage, to: target, targetKind: targetKind, quality: quality)
    }

    static func convertSVG(source: URL, target: URL, targetKind: FileKind, quality: Int) throws {
        guard let nsImage = NSImage(contentsOf: source) else {
            throw NativeError.readFailed("NSImage could not load SVG")
        }
        let intrinsic = nsImage.size
        // Render at 4× nominal size for crispness on downstream conversions.
        let scale: CGFloat = 4
        let pixelWidth  = max(1, Int(intrinsic.width  * scale))
        let pixelHeight = max(1, Int(intrinsic.height * scale))

        guard let bitmap = NSBitmapImageRep(
            bitmapDataPlanes: nil,
            pixelsWide: pixelWidth,
            pixelsHigh: pixelHeight,
            bitsPerSample: 8,
            samplesPerPixel: 4,
            hasAlpha: true,
            isPlanar: false,
            colorSpaceName: .deviceRGB,
            bytesPerRow: 0,
            bitsPerPixel: 0
        ) else {
            throw NativeError.writeFailed("Could not allocate bitmap")
        }
        bitmap.size = intrinsic

        NSGraphicsContext.saveGraphicsState()
        defer { NSGraphicsContext.restoreGraphicsState() }
        NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: bitmap)
        nsImage.draw(in: NSRect(origin: .zero, size: intrinsic))

        guard let cgImage = bitmap.cgImage else {
            throw NativeError.writeFailed("Could not extract CGImage")
        }
        try write(cgImage: cgImage, to: target, targetKind: targetKind, quality: quality)
    }

    static func rasterizePDF(source: URL, target: URL, targetKind: FileKind, dpi: Int, quality: Int) throws {
        guard let pdfDoc = CGPDFDocument(source as CFURL),
              let page = pdfDoc.page(at: 1) else {
            throw NativeError.readFailed("Could not open PDF")
        }
        let mediaBox = page.getBoxRect(.mediaBox)
        let scale = CGFloat(dpi) / 72.0
        let width  = max(1, Int(mediaBox.width  * scale))
        let height = max(1, Int(mediaBox.height * scale))

        guard let ctx = CGContext(
            data: nil,
            width: width, height: height,
            bitsPerComponent: 8, bytesPerRow: 0,
            space: CGColorSpaceCreateDeviceRGB(),
            bitmapInfo: CGImageAlphaInfo.premultipliedFirst.rawValue
        ) else {
            throw NativeError.writeFailed("Could not create bitmap context")
        }
        ctx.setFillColor(CGColor.white)
        ctx.fill(CGRect(x: 0, y: 0, width: width, height: height))
        ctx.scaleBy(x: scale, y: scale)
        ctx.drawPDFPage(page)

        guard let cgImage = ctx.makeImage() else {
            throw NativeError.writeFailed("Could not finalize CGImage")
        }
        try write(cgImage: cgImage, to: target, targetKind: targetKind, quality: quality)
    }

    // MARK: - Internals

    private static func write(cgImage: CGImage, to url: URL, targetKind: FileKind, quality: Int) throws {
        guard let utType = utType(for: targetKind) else {
            throw NativeError.unsupportedTargetFormat(targetKind)
        }
        guard let dest = CGImageDestinationCreateWithURL(url as CFURL, utType.identifier as CFString, 1, nil) else {
            throw NativeError.writeFailed("CGImageDestinationCreateWithURL returned nil for \(utType.identifier)")
        }
        let opts: [CFString: Any] = [
            kCGImageDestinationLossyCompressionQuality: Double(quality) / 100.0
        ]
        CGImageDestinationAddImage(dest, cgImage, opts as CFDictionary)
        if !CGImageDestinationFinalize(dest) {
            throw NativeError.writeFailed("CGImageDestinationFinalize failed for \(targetKind.displayName); this format may not be writable on this macOS version.")
        }
    }

    private static func utType(for kind: FileKind) -> UTType? {
        switch kind {
        case .jpeg: return .jpeg
        case .png:  return .png
        case .tiff: return .tiff
        case .bmp:  return .bmp
        case .gif:  return .gif
        case .heic: return .heic
        case .webp: return .webP
        case .avif:
            if #available(macOS 14.0, *) { return UTType("public.avif") }
            return nil
        case .ico:
            return UTType("com.microsoft.ico")
        default:
            return nil
        }
    }
}
