namespace Roslyn.Workbench.Mcp.CodeActions.Resolution.Requests;

/// <summary>
/// Identifies the document and source span used for Code Action discovery.
/// </summary>
internal sealed record CodeActionSourceSelection
{
    /// <summary>
    /// Gets the selected source document.
    /// </summary>
    public required Document Document { get; init; }

    /// <summary>
    /// Gets the selected span within the document.
    /// </summary>
    public required TextSpan Span { get; init; }
}
