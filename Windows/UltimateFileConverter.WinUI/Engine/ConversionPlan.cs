namespace UltimateFileConverter.WinUI.Engine;

/// <summary>
/// One command in a conversion pipeline. Arguments use the placeholders
/// <c>{INPUT}</c>, <c>{OUTPUT}</c>, and <c>{OUTPUT_DIR}</c>, resolved at run time.
/// Port of the macOS <c>ConversionStep</c>.
/// </summary>
public sealed class ConversionStep
{
    public ConversionStep(Tool tool, IReadOnlyList<string> argumentTemplate, string inputPath, string outputPath)
    {
        Tool = tool;
        ArgumentTemplate = argumentTemplate;
        InputPath = inputPath;
        OutputPath = outputPath;
    }

    public Tool Tool { get; }
    public IReadOnlyList<string> ArgumentTemplate { get; }
    public string InputPath { get; }
    public string OutputPath { get; }

    public IReadOnlyList<string> ResolveArguments()
    {
        var outputDir = System.IO.Path.GetDirectoryName(OutputPath) ?? System.IO.Directory.GetCurrentDirectory();
        return ArgumentTemplate
            .Select(token => token
                .Replace("{INPUT}", InputPath, System.StringComparison.Ordinal)
                .Replace("{OUTPUT}", OutputPath, System.StringComparison.Ordinal)
                .Replace("{OUTPUT_DIR}", outputDir, System.StringComparison.Ordinal))
            .ToArray();
    }
}

/// <summary>An ordered list of steps. Multi-step plans support pipelines (e.g. raw bytes → PNG → AVIF).</summary>
public sealed class ConversionPlan
{
    public ConversionPlan(params ConversionStep[] steps)
    {
        if (steps is null || steps.Length == 0)
        {
            throw new System.ArgumentException("A conversion plan requires at least one step.", nameof(steps));
        }
        Steps = steps;
    }

    public IReadOnlyList<ConversionStep> Steps { get; }

    public string InputPath => Steps[0].InputPath;
    public string OutputPath => Steps[^1].OutputPath;

    /// <summary>Convenience single-step factory mirroring the macOS initializer.</summary>
    public static ConversionPlan Single(Tool tool, IReadOnlyList<string> arguments, string input, string output) =>
        new(new ConversionStep(tool, arguments, input, output));
}
