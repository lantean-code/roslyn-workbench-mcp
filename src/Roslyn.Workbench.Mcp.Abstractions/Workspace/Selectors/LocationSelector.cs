namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

/// <summary>
/// Represents a location selector backed by either a text span or a copied selection.
/// </summary>
[Description("Provide exactly one of span or selection.")]
[RequiresExactlyOne(
    nameof(Span),
    nameof(Selection),
    ErrorMessage = "LocationSelector must provide exactly one of Span or Selection.")]
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
