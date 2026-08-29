namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

/// <summary>
/// Represents a document-bound text span selector.
/// </summary>
public sealed record TextSpanSelector
{
    /// <summary>
    /// Gets the selected document.
    /// </summary>
    [Description("The selected document.")]
    public required DocumentSelector Document { get; init; }

    /// <summary>
    /// Gets the selected range within the document.
    /// </summary>
    [Description("The selected range within the document.")]
    public required TextSpanRange Range { get; init; }
}
