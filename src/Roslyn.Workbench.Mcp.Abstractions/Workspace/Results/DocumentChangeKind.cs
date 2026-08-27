namespace Roslyn.Workbench.Mcp.Workspace.Results;

/// <summary>
/// Represents the kind of document change recorded in a change summary.
/// </summary>
public enum DocumentChangeKind
{
    /// <summary>
    /// The document was added.
    /// </summary>
    Added,

    /// <summary>
    /// The document was modified.
    /// </summary>
    Modified,

    /// <summary>
    /// The document was deleted.
    /// </summary>
    Deleted,
}
