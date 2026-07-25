namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

/// <summary>
/// Represents a resolved source location tied to a specific workspace snapshot.
/// </summary>
public sealed record ResolvedLocation
{
    /// <summary>
    /// Gets the workspace identifier associated with this location.
    /// </summary>
    public string? WorkspaceId { get; init; }

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
    /// Gets the workspace epoch associated with this location.
    /// </summary>
    public long WorkspaceEpoch { get; init; }

    /// <summary>
    /// Gets the transaction revision associated with this location, when available.
    /// </summary>
    public int? TransactionRevision { get; init; }
}
