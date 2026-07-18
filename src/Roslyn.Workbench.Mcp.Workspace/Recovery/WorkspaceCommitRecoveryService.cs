namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

internal sealed class WorkspaceCommitRecoveryService : IWorkspaceCommitRecoveryService
{
    private readonly ICommitRecoveryStore _store;
    private readonly IWorkspaceCommitWriter _writer;
    private readonly IWorkspaceCommitLockManager _lockManager;

    public WorkspaceCommitRecoveryService(
        ICommitRecoveryStore store,
        IWorkspaceCommitWriter writer,
        IWorkspaceCommitLockManager lockManager)
    {
        _store = store;
        _writer = writer;
        _lockManager = lockManager;
    }

    public async ValueTask RecoverAsync(CancellationToken cancellationToken)
    {
        foreach (var owner in await _store.GetOrphanedCommitOwnersAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var orphanLock = _lockManager.Acquire(owner.WorkspaceRoot);
            if (orphanLock.IsAcquired)
            {
                using var ownership = orphanLock.Lock;
                _store.DeleteStatus(owner.CommitId);
            }
        }

        foreach (var manifest in await _store.GetManifestsAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (manifest.State is RecoveryState.Committed or RecoveryState.Restored)
            {
                if (manifest.State == RecoveryState.Committed)
                {
                    if (!await _writer.CompleteAsync(manifest))
                    {
                        continue;
                    }
                }
                _store.DeleteStatus(manifest.CommitId);
                continue;
            }

            if (manifest.State == RecoveryState.RecoveryConflict)
            {
                continue;
            }

            var commitLock = _lockManager.Acquire(manifest.WorkspaceRoot);
            if (!commitLock.IsAcquired)
            {
                continue;
            }

            using var ownership = commitLock.Lock;

            var state = await _writer.RestoreAsync(manifest);
            var updated = manifest with { State = state };
            await _store.WriteManifestAsync(updated, CancellationToken.None);
            if (state == RecoveryState.Restored)
            {
                _store.DeleteStatus(manifest.CommitId);
            }
        }
    }
}
