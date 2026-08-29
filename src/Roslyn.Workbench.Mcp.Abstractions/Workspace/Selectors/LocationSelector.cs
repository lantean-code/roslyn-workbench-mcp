namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

/// <summary>
/// Represents a location selector backed by either a text span or a copied selection.
/// </summary>
[RequiresExactlyOne(
    nameof(Span),
    nameof(Selection),
    ErrorMessage = "LocationSelector must provide exactly one of Span or Selection.")]
public sealed record LocationSelector
{
    /// <summary>
    /// Gets the span-based selector.
    /// </summary>
    [Description("Document and UTF-16 span to use as the location; provide either span or selection, not both.")]
    public TextSpanSelector? Span { get; init; }

    /// <summary>
    /// Gets the copied-selection selector.
    /// </summary>
    [Description("Copied text with surrounding context to relocate; provide either selection or span, not both.")]
    public TextSelectionSelector? Selection { get; init; }
}
