using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

internal interface ICommitRecoveryStore
{
    ValueTask<IReadOnlyList<RecoveryStatus>> GetStatusesAsync(CancellationToken cancellationToken);

    ValueTask WriteStatusAsync(RecoveryStatus status, CancellationToken cancellationToken);

    void DeleteStatus(string commitId);

    ValueTask PersistPlanAsync(WorkspaceCommitPlan plan, CancellationToken cancellationToken);

    ValueTask WriteManifestAsync(WorkspaceCommitManifest manifest, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<WorkspaceCommitManifest>> GetManifestsAsync(CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<WorkspaceCommitOwner>> GetOrphanedCommitOwnersAsync(CancellationToken cancellationToken);

    ValueTask<byte[]> ReadArtifactAsync(string commitId, string relativePath, CancellationToken cancellationToken);
}
