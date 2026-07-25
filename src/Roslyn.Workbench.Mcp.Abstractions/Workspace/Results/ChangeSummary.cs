namespace Roslyn.Workbench.Mcp.Workspace.Results;

/// <summary>
/// Represents the cross-cutting source changes recorded for a mutation.
/// </summary>
public sealed record ChangeSummary
{
    /// <summary>
    /// Gets the documents added by the operation.
    /// </summary>
    public IReadOnlyList<DocumentChange> Added { get; init; } = [];

    /// <summary>
    /// Gets the documents modified by the operation.
    /// </summary>
    public IReadOnlyList<DocumentChange> Modified { get; init; } = [];

    /// <summary>
    /// Gets the documents deleted by the operation.
    /// </summary>
    public IReadOnlyList<DocumentChange> Deleted { get; init; } = [];

    /// <summary>
    /// Gets the affected symbols associated with the change.
    /// </summary>
    public IReadOnlyList<SymbolReference> AffectedSymbols { get; init; } = [];
}
