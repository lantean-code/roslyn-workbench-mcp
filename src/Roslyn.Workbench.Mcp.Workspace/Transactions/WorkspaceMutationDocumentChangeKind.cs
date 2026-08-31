namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Classifies how a mutation candidate changes one source document.
/// </summary>
internal enum WorkspaceMutationDocumentChangeKind
{
    /// <summary>
    /// The candidate adds a source document.
    /// </summary>
    Added,
    /// <summary>
    /// The candidate changes an existing source document.
    /// </summary>
    Modified,
    /// <summary>
    /// The candidate removes a source document.
    /// </summary>
    Deleted,
}
