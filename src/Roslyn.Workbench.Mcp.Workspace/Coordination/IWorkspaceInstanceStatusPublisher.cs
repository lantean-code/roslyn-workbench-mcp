namespace Roslyn.Workbench.Mcp.Workspace.Coordination;

internal interface IWorkspaceInstanceStatusPublisher
{
    ValueTask<WorkspaceInstanceStatusResult> OpenAsync(
        Guid workspaceId,
        string workspaceRoot,
        string loadedPath,
        WorkspaceLifecycleState state,
        CancellationToken cancellationToken);

    ValueTask UpdateAsync(Guid workspaceId, WorkspaceLifecycleState state, long? transactionRevision, string? commitId, string? commitPhase);

    void QueueUpdate(Guid workspaceId, WorkspaceLifecycleState state, long? transactionRevision, string? commitId, string? commitPhase);

    ValueTask<WorkspaceInstanceStatusResult> GetOtherLiveInstancesAsync(
        string workspaceRoot,
        CancellationToken cancellationToken);

    ValueTask CloseAsync(Guid workspaceId);
}
