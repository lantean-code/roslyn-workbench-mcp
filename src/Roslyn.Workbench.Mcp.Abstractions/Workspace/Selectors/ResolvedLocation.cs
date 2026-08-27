namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

/// <summary>
/// Represents a resolved source location tied to a specific workspace snapshot.
/// </summary>
public sealed record ResolvedLocation
{
    /// <summary>
    /// Gets the resolved document.
    /// </summary>
    public DocumentReference? Document { get; init; }

    /// <summary>
    /// Gets the resolved text span.
    /// </summary>
    public TextSpanRange? Span { get; init; }

    /// <summary>
    /// Gets the one-based line number.
    /// </summary>
    public int Line { get; init; }

    /// <summary>
    /// Gets the one-based column number.
    /// </summary>
    public int Column { get; init; }

    /// <summary>
    /// Gets the exact immutable workspace snapshot associated with this location.
    /// </summary>
    public required SnapshotPrecondition Snapshot { get; init; }

    /// <summary>
    /// Gets the canonical selector that can resolve this source location again.
    /// </summary>
    public CanonicalLocationSelector? Selector { get; init; }
}
