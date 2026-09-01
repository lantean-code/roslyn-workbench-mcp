namespace Roslyn.Workbench.Mcp.ToolReferenceGenerator;

/// <summary>
/// Identifies the output and authored-example inputs used for one documentation generation run.
/// </summary>
internal sealed class ToolReferenceGeneratorOptions
{
    /// <summary>
    /// Gets the directory that receives generated reference files.
    /// </summary>
    public required string OutputDirectory { get; init; }

    /// <summary>
    /// Gets the file containing canonical tool-call examples.
    /// </summary>
    public required string ExamplesFile { get; init; }

    /// <summary>
    /// Parses and validates generator command-line arguments.
    /// </summary>
    /// <param name="args">The arguments supplied to the generator.</param>
    /// <returns>The validated generator options.</returns>
    public static ToolReferenceGeneratorOptions Parse(IReadOnlyList<string> args)
    {
        string? outputDirectory = null;
        string? examplesFile = null;

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (index + 1 >= args.Count)
            {
                throw new ArgumentException($"Generator option '{argument}' requires a value.", nameof(args));
            }

            var value = args[++index];
            switch (argument)
            {
                case "--output":
                    outputDirectory = value;
                    break;
                case "--examples":
                    examplesFile = value;
                    break;
                default:
                    throw new ArgumentException($"Generator option '{argument}' is not supported.", nameof(args));
            }
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(examplesFile);

        return new ToolReferenceGeneratorOptions
        {
            OutputDirectory = Path.GetFullPath(outputDirectory),
            ExamplesFile = Path.GetFullPath(examplesFile),
        };
    }
}
