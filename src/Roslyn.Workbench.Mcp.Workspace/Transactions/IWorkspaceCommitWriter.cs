namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal interface IWorkspaceCommitWriter
{
    ValueTask RevalidateAsync(WorkspaceCommitManifest manifest, CancellationToken cancellationToken);

    ValueTask ApplyAsync(WorkspaceCommitManifest manifest);

    ValueTask<bool> CompleteAsync(WorkspaceCommitManifest manifest);

    ValueTask<RecoveryState> RestoreAsync(WorkspaceCommitManifest manifest);
}
