namespace Roslyn.Workbench.Mcp.ToolReferenceGenerator;

/// <summary>
/// Provides the command-line entry point for production tool-reference generation.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Generates the reference files requested by the command line.
    /// </summary>
    /// <param name="args">The generator command-line arguments.</param>
    /// <returns>Zero when generation succeeds; otherwise, a non-zero exit code.</returns>
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var options = ToolReferenceGeneratorOptions.Parse(args);
            var generator = new ToolReferenceGenerator();
            await generator.GenerateAsync(options, CancellationToken.None);
            return 0;
        }
        catch (Exception exception)
        {
            await Console.Error.WriteLineAsync(exception.Message);
            return 1;
        }
    }
}
