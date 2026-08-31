namespace Roslyn.Workbench.Mcp.Workspace.ExecutionContexts;

/// <summary>
/// Selects and validates workspace sessions before creating leased query or mutation contexts.
/// </summary>
internal interface IWorkspaceExecutionContextFactory
{
    /// <summary>
    /// Creates a shared, leased execution context for a query.
    /// </summary>
    /// <param name="workspace">The optional workspace selector.</param>
    /// <param name="cancellationToken">The token used to cancel context creation.</param>
    /// <returns>An acquired context or a classified rejection.</returns>
    WorkspaceExecutionContextLease CreateQueryContext(
        WorkspaceSelector? workspace,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates an exclusive, leased execution context and stager for a mutation.
    /// </summary>
    /// <param name="workspace">The optional workspace selector.</param>
    /// <param name="expectedSnapshot">The snapshot that the caller expects to mutate.</param>
    /// <param name="cancellationToken">The token used to cancel context creation.</param>
    /// <returns>An acquired mutation context or a classified rejection.</returns>
    WorkspaceMutationExecutionLease CreateMutationContext(
        WorkspaceSelector? workspace,
        SnapshotPrecondition expectedSnapshot,
        CancellationToken cancellationToken);

    /// <summary>
    /// Detects whether Roslyn changed a loaded workspace outside the controlled transaction pipeline.
    /// </summary>
    /// <param name="workspaceId">The workspace to inspect.</param>
    /// <returns>A failure when the workspace is unavailable after detection; otherwise, <see langword="null"/>.</returns>
    WorkspaceExecutionFailure? DetectUnexpectedWorkspaceChange(Guid workspaceId);
}
