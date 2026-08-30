namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

/// <summary>
/// Represents a document-bound text span selector.
/// </summary>
public sealed record TextSpanSelector
{
    /// <summary>
    /// Gets the selected document.
    /// </summary>
    public required DocumentSelector Document { get; init; }

    /// <summary>
    /// Gets the selected range within the document.
    /// </summary>
    [Description("Zero-based UTF-16 range.")]
    public required TextSpanRange Range { get; init; }
}
