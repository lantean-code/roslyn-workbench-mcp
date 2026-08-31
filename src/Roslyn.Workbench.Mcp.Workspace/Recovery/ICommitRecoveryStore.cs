namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

/// <summary>
/// Persists and retrieves durable evidence used to recover interrupted workspace commits.
/// </summary>
internal interface ICommitRecoveryStore
{
    /// <summary>
    /// Gets all persisted recovery statuses, including legacy and malformed records.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the statuses.</returns>
    ValueTask<IReadOnlyList<RecoveryStatus>> GetStatusesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Writes a legacy recovery status record.
    /// </summary>
    /// <param name="status">The status value to expose in the result.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    ValueTask WriteStatusAsync(RecoveryStatus status, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes all persisted recovery evidence for a commit.
    /// </summary>
    /// <param name="commitId">The commit identifier.</param>
    void DeleteStatus(string commitId);

    /// <summary>
    /// Persists the complete recovery plan before a workspace commit begins.
    /// </summary>
    /// <param name="plan">The commit or recovery plan produced by the preceding operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the commit recovery plan persistence result.</returns>
    ValueTask<CommitRecoveryPlanPersistenceResult> PersistPlanAsync(
        WorkspaceCommitPlan plan,
        CancellationToken cancellationToken);

    /// <summary>
    /// Writes the current state of a persisted recovery manifest.
    /// </summary>
    /// <param name="manifest">The manifest whose contents are being processed.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    ValueTask WriteManifestAsync(WorkspaceCommitManifest manifest, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the valid and conflict-marked recovery manifests.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the manifests.</returns>
    ValueTask<IReadOnlyList<WorkspaceCommitManifest>> GetManifestsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets valid commit owner records that do not yet have a manifest.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the orphaned commit owners.</returns>
    ValueTask<IReadOnlyList<WorkspaceCommitOwner>> GetOrphanedCommitOwnersAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reads a persisted recovery artifact for a commit.
    /// </summary>
    /// <param name="commitId">The commit identifier.</param>
    /// <param name="relativePath">The workspace-relative path of the recovery artifact to read.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the persisted recovery artifact content.</returns>
    ValueTask<byte[]> ReadArtifactAsync(string commitId, string relativePath, CancellationToken cancellationToken);
}
