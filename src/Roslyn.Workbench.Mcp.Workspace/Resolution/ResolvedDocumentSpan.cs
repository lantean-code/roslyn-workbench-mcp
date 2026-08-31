namespace Roslyn.Workbench.Mcp.Workspace.Resolution;

/// <summary>
/// Identifies a source span within a resolved Roslyn document.
/// </summary>
internal sealed record ResolvedDocumentSpan
{
    /// <summary>
    /// Gets the resolved source document.
    /// </summary>
    public required Document Document { get; init; }

    /// <summary>
    /// Gets the selected span within the document.
    /// </summary>
    public TextSpan Span { get; init; }
}
