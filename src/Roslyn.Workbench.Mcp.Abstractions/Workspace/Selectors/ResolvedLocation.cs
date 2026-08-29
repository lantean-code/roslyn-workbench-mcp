namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

/// <summary>
/// Represents a resolved source location tied to a specific workspace snapshot.
/// </summary>
public sealed record ResolvedLocation
{
    /// <summary>
    /// Gets the resolved document.
    /// </summary>
    [Description("The resolved document.")]
    public DocumentReference? Document { get; init; }

    /// <summary>
    /// Gets the resolved text span.
    /// </summary>
    [Description("The resolved text span.")]
    public TextSpanRange? Span { get; init; }

    /// <summary>
    /// Gets the one-based line number.
    /// </summary>
    [Description("The one-based line number.")]
    public int Line { get; init; }

    /// <summary>
    /// Gets the one-based column number.
    /// </summary>
    [Description("The one-based column number.")]
    public int Column { get; init; }

    /// <summary>
    /// Gets the exact immutable workspace snapshot associated with this location.
    /// </summary>
    [Description("The exact immutable workspace snapshot associated with this location.")]
    public required SnapshotPrecondition Snapshot { get; init; }

    /// <summary>
    /// Gets the canonical selector that can resolve this source location again.
    /// </summary>
    [Description("The canonical selector that can resolve this source location again.")]
    public CanonicalLocationSelector? Selector { get; init; }
}
