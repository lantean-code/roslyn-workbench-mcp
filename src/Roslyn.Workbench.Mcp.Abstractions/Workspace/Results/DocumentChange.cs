namespace Roslyn.Workbench.Mcp.Workspace.Results;

/// <summary>
/// Represents a document change recorded in a tool result.
/// </summary>
public sealed record DocumentChange
{
    /// <summary>
    /// Gets the document that changed.
    /// </summary>
    [Description("The document that changed.")]
    public DocumentReference? Document { get; init; }

    /// <summary>
    /// Gets the kind of change applied to the document.
    /// </summary>
    [Description("The kind of change applied to the document.")]
    public DocumentChangeKind ChangeKind { get; init; }

    /// <summary>
    /// Gets the optional diff summary for the change.
    /// </summary>
    [Description("The optional diff summary for the change.")]
    public DiffSummary? Preview { get; init; }
}
