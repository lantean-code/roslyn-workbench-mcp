namespace Roslyn.Workbench.Mcp.Workspace.Results;

/// <summary>
/// Represents a document change recorded in a tool result.
/// </summary>
public sealed record DocumentChange
{
    /// <summary>
    /// Gets the document that changed.
    /// </summary>
    public DocumentReference? Document { get; init; }

    /// <summary>
    /// Gets the kind of change applied to the document.
    /// </summary>
    public DocumentChangeKind ChangeKind { get; init; }

    /// <summary>
    /// Gets the optional diff summary for the change.
    /// </summary>
    public DiffSummary? Preview { get; init; }
}
