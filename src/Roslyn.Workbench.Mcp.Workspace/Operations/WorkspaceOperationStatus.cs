namespace Roslyn.Workbench.Mcp.Workspace.Operations;

/// <summary>
/// Classifies the outcome of a workspace operation.
/// </summary>
internal enum WorkspaceOperationStatus
{
    /// <summary>
    /// The operation completed and produced its requested outcome.
    /// </summary>
    Succeeded,
    /// <summary>
    /// The request was not valid or its prerequisites were not satisfied.
    /// </summary>
    Rejected,
    /// <summary>
    /// The request conflicted with current workspace or transaction state.
    /// </summary>
    Conflict,
    /// <summary>
    /// The operation encountered an unexpected failure.
    /// </summary>
    Faulted,
    /// <summary>
    /// The operation completed successfully without changing workspace state.
    /// </summary>
    NoChange,
}
