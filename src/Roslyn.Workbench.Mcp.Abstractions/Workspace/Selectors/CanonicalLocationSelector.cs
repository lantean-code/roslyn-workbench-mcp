namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

/// <summary>
/// Represents the canonical span-based selector for a resolved source location.
/// </summary>
public sealed record CanonicalLocationSelector
{
    /// <summary>
    /// Gets the document-bound text span.
    /// </summary>
    [Description("The document-bound text span.")]
    public required TextSpanSelector Span { get; init; }
}
