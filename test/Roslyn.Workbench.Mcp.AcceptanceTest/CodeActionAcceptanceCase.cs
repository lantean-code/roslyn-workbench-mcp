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
        string? diagnosticId = null)
    {
        ToolName = toolName;
        DisplayName = string.IsNullOrWhiteSpace(diagnosticId)
            ? toolName
            : $"{toolName}/{diagnosticId}";

        DiagnosticId = diagnosticId;
        Arguments = arguments;
        ExpectedDocumentPaths = expectedDocumentPaths;
    }
}
