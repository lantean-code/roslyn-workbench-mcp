namespace Roslyn.Workbench.Mcp.Workspace.Coordination;

/// <summary>
/// Maintains the on-disk record used to coordinate concurrent server instances for a workspace.
/// </summary>
internal interface IWorkspaceInstanceStatusPublisher
{
    /// <summary>
    /// Creates this process's live instance record and reports any competing instances.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="workspaceRoot">The workspace root path.</param>
    /// <param name="loadedPath">The path loaded into the workspace.</param>
    /// <param name="state">The initial workspace lifecycle state.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the availability and competing-instance status.</returns>
    ValueTask<WorkspaceInstanceStatusResult> OpenAsync(
        Guid workspaceId,
        string workspaceRoot,
        string loadedPath,
        WorkspaceLifecycleState state,
        CancellationToken cancellationToken);

    /// <summary>
    /// Writes the current lifecycle and commit state to this process's instance record.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="state">The current workspace lifecycle state.</param>
    /// <param name="transactionRevision">The current transaction revision, when a transaction is active.</param>
    /// <param name="commitId">The commit identifier.</param>
    /// <param name="commitPhase">The commit phase in which the operation is running.</param>
    /// <returns>A task that completes when the instance record has been updated.</returns>
    ValueTask UpdateAsync(Guid workspaceId, WorkspaceLifecycleState state, long? transactionRevision, string? commitId, string? commitPhase);

    /// <summary>
    /// Queues a non-blocking update to this process's instance record.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="state">The current workspace lifecycle state.</param>
    /// <param name="transactionRevision">The current transaction revision, when a transaction is active.</param>
    /// <param name="commitId">The commit identifier.</param>
    /// <param name="commitPhase">The commit phase in which the operation is running.</param>
    void QueueUpdate(Guid workspaceId, WorkspaceLifecycleState state, long? transactionRevision, string? commitId, string? commitPhase);

    /// <summary>
    /// Finds other live server instances that advertise ownership of the workspace root.
    /// </summary>
    /// <param name="workspaceRoot">The workspace root path.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the availability and competing-instance status.</returns>
    ValueTask<WorkspaceInstanceStatusResult> GetOtherLiveInstancesAsync(
        string workspaceRoot,
        CancellationToken cancellationToken);

    /// <summary>
    /// Closes and removes this process's live instance record.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <returns>A task that completes when the record has been closed.</returns>
    ValueTask CloseAsync(Guid workspaceId);
}
