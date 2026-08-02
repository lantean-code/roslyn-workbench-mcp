namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal interface IWorkspaceCommitWriter
{
    ValueTask<WorkspaceCommitValidationResult> RevalidateAsync(
        WorkspaceCommitManifest manifest,
        CancellationToken cancellationToken);

    ValueTask<WorkspaceCommitValidationResult> ApplyAsync(WorkspaceCommitManifest manifest);

    ValueTask<WorkspaceCommitValidationResult> ValidateAppliedStateAsync(WorkspaceCommitManifest manifest);

    ValueTask<bool> CompleteAsync(WorkspaceCommitManifest manifest);

    ValueTask<RecoveryState> RestoreAsync(WorkspaceCommitManifest manifest);
}
