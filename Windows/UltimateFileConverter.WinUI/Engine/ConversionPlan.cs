namespace UltimateFileConverter.WinUI.Engine;

public sealed record ConversionStep(Tool Tool, IReadOnlyList<string> ArgumentTemplate, string InputPath, string OutputPath)
{
    public IReadOnlyList<string> ResolveArguments()
    {
        var outDir = Path.GetDirectoryName(OutputPath) ?? Directory.GetCurrentDirectory();
        return ArgumentTemplate
            .Select(token => token
                .Replace("{INPUT}", InputPath, StringComparison.Ordinal)
                .Replace("{OUTPUT}", OutputPath, StringComparison.Ordinal)
                .Replace("{OUTPUT_DIR}", outDir, StringComparison.Ordinal))
            .ToArray();
    }
}

public sealed class ConversionPlan
{
    public ConversionPlan(params ConversionStep[] steps)
    {
        if (steps.Length == 0) throw new ArgumentException("A conversion plan requires at least one step.", nameof(steps));
        Steps = steps;
    }

    public IReadOnlyList<ConversionStep> Steps { get; }
    public string InputPath => Steps.First().InputPath;
    public string OutputPath => Steps.Last().OutputPath;
}
