namespace Roslyn.Workbench.Mcp.Workspace.Coordination;

internal interface IWorkspaceInstanceStatusPublisher
{
    ValueTask<bool> OpenAsync(
        string workspaceId,
        string workspaceRoot,
        string loadedPath,
        WorkspaceLifecycleState state,
        CancellationToken cancellationToken);

    ValueTask UpdateAsync(string workspaceId, WorkspaceLifecycleState state, long? transactionRevision, string? commitId, string? commitPhase);

    ValueTask<IReadOnlyList<WorkspaceInstanceInfo>> GetOtherLiveInstancesAsync(
        string workspaceRoot,
        CancellationToken cancellationToken);

    void Close(string workspaceId);
}
