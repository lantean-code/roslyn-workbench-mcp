namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

/// <summary>
/// Represents a location selector backed by either a text span or a copied selection.
/// </summary>
public sealed record LocationSelector
{
    /// <summary>
    /// Gets the span-based selector.
    /// </summary>
    public TextSpanSelector? Span { get; init; }

    /// <summary>
    /// Gets the copied-selection selector.
    /// </summary>
    public TextSelectionSelector? Selection { get; init; }
}
