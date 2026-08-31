namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Applies, validates, completes, or restores a durable workspace commit manifest.
/// </summary>
internal interface IWorkspaceCommitWriter
{
    /// <summary>
    /// Revalidates baseline files immediately before commit application.
    /// </summary>
    /// <param name="manifest">The manifest whose contents are being processed.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the workspace commit validation result.</returns>
    ValueTask<WorkspaceCommitValidationResult> RevalidateAsync(
        WorkspaceCommitManifest manifest,
        CancellationToken cancellationToken);

    /// <summary>
    /// Applies the planned file operations and advances the durable manifest.
    /// </summary>
    /// <param name="manifest">The manifest whose contents are being processed.</param>
    /// <returns>A task that completes with the workspace commit validation result.</returns>
    ValueTask<WorkspaceCommitValidationResult> ApplyAsync(WorkspaceCommitManifest manifest);

    /// <summary>
    /// Verifies that applied files match the manifest's intended state.
    /// </summary>
    /// <param name="manifest">The manifest whose contents are being processed.</param>
    /// <returns>A task that completes with the workspace commit validation result.</returns>
    ValueTask<WorkspaceCommitValidationResult> ValidateAppliedStateAsync(WorkspaceCommitManifest manifest);

    /// <summary>
    /// Removes recovery artifacts after a successfully validated commit.
    /// </summary>
    /// <param name="manifest">The manifest whose contents are being processed.</param>
    /// <returns>A task that completes with <see langword="true"/> when commit cleanup succeeds; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> CompleteAsync(WorkspaceCommitManifest manifest);

    /// <summary>
    /// Restores original files for an incomplete or failed commit.
    /// </summary>
    /// <param name="manifest">The manifest whose contents are being processed.</param>
    /// <returns>A task that completes with the recovery state.</returns>
    ValueTask<RecoveryState> RestoreAsync(WorkspaceCommitManifest manifest);
}
