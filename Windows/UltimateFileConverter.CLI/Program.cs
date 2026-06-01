using UltimateFileConverter.WinUI.Engine;
using UltimateFileConverter.WinUI.Models;

namespace UltimateFileConverter.CLI;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return 2;
        }

        return args[0] switch
        {
            "version"      => PrintVersion(),
            "list-targets" => await ListTargetsAsync(args[1..]),
            "convert"      => await ConvertAsync(args[1..]),
            "--help" or "-h" or "help" => PrintHelp(),
            _ => UnknownCommand(args[0]),
        };
    }

    private static int PrintHelp()
    {
        Console.WriteLine("""
            Usage: ufc <command> [options]

            Commands:
              convert <input> <output> [--quality N]
                  Convert a file. Exit 0 on success, 1 on failure, 2 on bad arguments.
              list-targets <input>
                  Print valid conversion targets for a file.
              version
                  Print version.
            """);
        return 0;
    }

    private static int PrintVersion()
    {
        Console.WriteLine("ULTIMATE-FILE-CONVERTER ufc 1.2.0");
        return 0;
    }

    private static int UnknownCommand(string name)
    {
        Console.Error.WriteLine($"ufc: unknown command '{name}'");
        PrintHelp();
        return 2;
    }

    private static async Task<int> ListTargetsAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("ufc list-targets: missing input file");
            return 2;
        }

        var inputPath = args[0];
        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"ufc: file not found: {inputPath}");
            return 1;
        }

        var kind = FormatDetector.Detect(inputPath);
        if (kind is not FileKind sourceKind)
        {
            Console.Error.WriteLine($"ufc: unrecognized format: {inputPath}");
            return 1;
        }

        var settings = new ConversionSettings();
        var targets = ConversionRouter.ValidTargets(sourceKind, settings);
        if (targets.Count == 0)
        {
            Console.WriteLine($"No conversion targets for {sourceKind.DisplayName()}.");
        }
        else
        {
            Console.WriteLine($"Targets for {sourceKind.DisplayName()}:");
            foreach (var target in targets)
                Console.WriteLine($"  {target.CanonicalExtension()}  ({target.DisplayName()})");
        }

        return await Task.FromResult(0);
    }

    private static async Task<int> ConvertAsync(string[] args)
    {
        var positional = new List<string>();
        var settings = new ConversionSettings();

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--quality")
            {
                if (++i >= args.Length || !int.TryParse(args[i], out var q) || q < 1 || q > 100)
                {
                    Console.Error.WriteLine("ufc: --quality requires an integer between 1 and 100");
                    return 2;
                }
                settings.ImageQuality = q;
            }
            else
            {
                positional.Add(args[i]);
            }
        }

        if (positional.Count < 2)
        {
            Console.Error.WriteLine("ufc convert: requires <input> <output>");
            return 2;
        }

        var inputPath  = positional[0];
        var outputPath = positional[1];

        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"ufc: file not found: {inputPath}");
            return 1;
        }

        var sourceKind = FormatDetector.Detect(inputPath);
        if (sourceKind is null)
        {
            Console.Error.WriteLine($"ufc: unrecognized source format: {inputPath}");
            return 1;
        }

        var outputExt  = Path.GetExtension(outputPath).TrimStart('.').ToLowerInvariant();
        var targetKind = Formats.All.FirstOrDefault(k =>
            k.CanonicalExtension() == outputExt || k.RecognizedExtensions().Contains(outputExt));
        if (targetKind == default || targetKind.IsSourceOnly())
        {
            Console.Error.WriteLine($"ufc: unrecognized or unsupported target format: {outputExt}");
            return 1;
        }

        var plan = ConversionRouter.Plan(sourceKind.Value, targetKind, inputPath, outputPath, settings);
        if (plan is null)
        {
            Console.Error.WriteLine(
                $"ufc: no conversion path from {sourceKind.Value.DisplayName()} to {targetKind.DisplayName()}");
            return 1;
        }

        try
        {
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

            var result = await ToolRunner.ExecuteAsync(plan, cts.Token);
            if (result.Success && File.Exists(outputPath))
            {
                Console.WriteLine($"✓ {outputPath}");
                return 0;
            }
            else
            {
                var msg = string.IsNullOrEmpty(result.StandardError)
                    ? result.StandardOutput
                    : result.StandardError;
                Console.Error.WriteLine(
                    $"ufc: conversion failed{(string.IsNullOrWhiteSpace(msg) ? "" : $": {msg.Trim()}")}");
                return 1;
            }
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("ufc: conversion cancelled");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ufc: {ex.Message}");
            return 1;
        }
    }
}
