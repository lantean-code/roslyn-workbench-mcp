namespace Roslyn.Workbench.Mcp.Workspace.Coordination;

internal interface IWorkspaceInstanceStatusPublisher
{
    ValueTask<WorkspaceInstanceStatusResult> OpenAsync(
        string workspaceId,
        string workspaceRoot,
        string loadedPath,
        WorkspaceLifecycleState state,
        CancellationToken cancellationToken);

    ValueTask UpdateAsync(string workspaceId, WorkspaceLifecycleState state, long? transactionRevision, string? commitId, string? commitPhase);

    ValueTask<WorkspaceInstanceStatusResult> GetOtherLiveInstancesAsync(
        string workspaceRoot,
        CancellationToken cancellationToken);

    ValueTask CloseAsync(string workspaceId);
}
