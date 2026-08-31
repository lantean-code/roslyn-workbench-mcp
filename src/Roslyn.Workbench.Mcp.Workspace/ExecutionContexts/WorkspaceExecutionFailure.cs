namespace Roslyn.Workbench.Mcp.Workspace.ExecutionContexts;

/// <summary>
/// Describes why a workspace execution context could not be acquired or used.
/// </summary>
internal sealed record WorkspaceExecutionFailure
{
    /// <summary>
    /// Gets the operation status associated with the failure.
    /// </summary>
    public WorkspaceOperationStatus Status { get; init; }

    /// <summary>
    /// Gets the structured workspace error.
    /// </summary>
    public WorkspaceOperationError Error { get; init; } = new();
}
