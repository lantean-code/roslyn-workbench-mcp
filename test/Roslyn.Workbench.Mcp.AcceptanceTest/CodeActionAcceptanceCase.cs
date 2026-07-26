namespace Roslyn.Workbench.Mcp.AcceptanceTest;

internal sealed record CodeActionAcceptanceCase
{
    public string ToolName { get; }

    public IReadOnlyDictionary<string, object?> Arguments { get; }

    public IReadOnlyList<string> ExpectedDocumentPaths { get; }

    public CodeActionAcceptanceCase(
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        IReadOnlyList<string> expectedDocumentPaths)
    {
        ToolName = toolName;
        Arguments = arguments;
        ExpectedDocumentPaths = expectedDocumentPaths;
    }
}
