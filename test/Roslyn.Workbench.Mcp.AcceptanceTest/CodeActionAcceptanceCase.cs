namespace Roslyn.Workbench.Mcp.AcceptanceTest;

internal sealed record CodeActionAcceptanceCase
{
    public string ToolName { get; }

    public string DisplayName { get; }

    public string? DiagnosticId { get; }

    public IReadOnlyDictionary<string, object?> Arguments { get; }

    public IReadOnlyList<string> ExpectedDocumentPaths { get; }

    public CodeActionAcceptanceCase(
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        IReadOnlyList<string> expectedDocumentPaths,
        string? diagnosticId = null,
        string? variant = null)
    {
        ToolName = toolName;
        DisplayName = CreateDisplayName(toolName, diagnosticId, variant);

        DiagnosticId = diagnosticId;
        Arguments = arguments;
        ExpectedDocumentPaths = expectedDocumentPaths;
    }

    private static string CreateDisplayName(string toolName, string? diagnosticId, string? variant)
    {
        if (string.IsNullOrWhiteSpace(diagnosticId))
        {
            return toolName;
        }

        return string.IsNullOrWhiteSpace(variant)
            ? $"{toolName}/{diagnosticId}"
            : $"{toolName}/{diagnosticId}/{variant}";
    }
}
