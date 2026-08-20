using Roslyn.Workbench.Mcp.Workspace.Caching;

namespace Roslyn.Workbench.Mcp.Workspace.State;

internal sealed class WorkspaceSessionStore : IWorkspaceSessionStore
{
    private readonly IReadOnlyList<IWorkspaceSnapshotLifecycleObserver> _lifecycleObservers;
    private readonly IWorkspaceQueryCache _queryCache;
    private readonly Lock _syncRoot;
    private WorkspaceHostSnapshot _snapshot;
    private long _nextWorkspaceEpoch;
    private long _nextWorkspaceSnapshotId;
    private long _nextWorkspaceTransactionId;

    public WorkspaceSessionStore(
        IWorkspaceQueryCache queryCache,
        IEnumerable<IWorkspaceSnapshotLifecycleObserver> lifecycleObservers)
    {
        _lifecycleObservers = lifecycleObservers.ToArray();
        _queryCache = queryCache;
        _syncRoot = new Lock();
        _snapshot = new WorkspaceHostSnapshot();
    }

    public WorkspaceHostSnapshot ReadSnapshot()
    {
        lock (_syncRoot)
        {
            return _snapshot;
        }
    }

    public WorkspaceSessionSnapshot? ReadSession(Guid workspaceId)
    {
        lock (_syncRoot)
        {
            return _snapshot.Workspaces.TryGetValue(workspaceId, out var session)
                ? session
                : null;
        }
    }

    public Guid AllocateWorkspaceId()
    {
        return Guid.NewGuid();
    }

    public long AllocateWorkspaceEpoch()
    {
        return Interlocked.Increment(ref _nextWorkspaceEpoch);
    }

    public WorkspaceSnapshotId AllocateWorkspaceSnapshotId()
    {
        var value = Interlocked.Increment(ref _nextWorkspaceSnapshotId);
        return new WorkspaceSnapshotId(value);
    }

    public WorkspaceTransactionId AllocateWorkspaceTransactionId()
    {
        var value = Interlocked.Increment(ref _nextWorkspaceTransactionId);
        return new WorkspaceTransactionId(value);
    }

    public WorkspaceOperationError? TryAddWorkspace(WorkspaceSessionSnapshot session, Func<WorkspaceHostSnapshot, WorkspaceOperationError?> validate)
    {
        lock (_syncRoot)
        {
            var validationError = validate(_snapshot);
            if (validationError is not null)
            {
                return validationError;
            }

            var workspaces = new Dictionary<Guid, WorkspaceSessionSnapshot>(_snapshot.Workspaces)
            {
                [session.Workspace.WorkspaceId] = session,
            };

            _snapshot = _snapshot with
            {
                Workspaces = workspaces,
            };

            return null;
        }
    }

    public WorkspaceSessionSnapshot? RemoveWorkspace(Guid workspaceId)
    {
        WorkspaceSessionSnapshot? session;
        lock (_syncRoot)
        {
            if (!_snapshot.Workspaces.TryGetValue(workspaceId, out session))
            {
                return null;
            }

            InvalidateWorkspace(session);

            var workspaces = new Dictionary<Guid, WorkspaceSessionSnapshot>(_snapshot.Workspaces);
            workspaces.Remove(workspaceId);
            _snapshot = _snapshot with
            {
                Workspaces = workspaces,
                TransactionOwnerWorkspaceId = _snapshot.TransactionOwnerWorkspaceId == workspaceId
                    ? null
                    : _snapshot.TransactionOwnerWorkspaceId,
            };
        }

        _queryCache.InvalidateWorkspace(workspaceId);
        return session;
    }

    public IReadOnlyList<WorkspaceSessionSnapshot> DrainWorkspaces()
    {
        lock (_syncRoot)
        {
            var sessions = _snapshot.Workspaces.Values
                .OrderBy(static session => session.Workspace.WorkspaceId)
                .ToArray();

            _snapshot = new WorkspaceHostSnapshot();
            return sessions;
        }
    }

    public void ReplaceSession(WorkspaceSessionSnapshot session)
    {
        ReplaceSessionCore(session, []);
    }

    public void ReplaceSessionAfterStaging(
        WorkspaceSessionSnapshot session,
        IReadOnlyList<WorkspaceSnapshotId> discardedSnapshotIds)
    {
        ReplaceSessionCore(session, discardedSnapshotIds);
    }

    public TransactionAdmissionResult TryStartTransaction(WorkspaceSessionSnapshot session)
    {
        bool invalidateQueryCache;
        lock (_syncRoot)
        {
            if (_snapshot.TransactionOwnerWorkspaceId is { } existingOwnerWorkspaceId)
            {
                return TransactionAdmissionResult.Rejected(existingOwnerWorkspaceId);
            }

            _snapshot.Workspaces.TryGetValue(session.Workspace.WorkspaceId, out var previousSession);
            NotifySnapshotLifecycle(previousSession, session, []);
            invalidateQueryCache = ReplaceSessionLocked(session);
            _snapshot = _snapshot with
            {
                TransactionOwnerWorkspaceId = session.Workspace.WorkspaceId,
            };
        }

        if (invalidateQueryCache)
        {
            _queryCache.InvalidateWorkspace(session.Workspace.WorkspaceId);
        }

        return TransactionAdmissionResult.Admitted();
    }

    public TransactionCompletionResult TryCompleteTransaction(WorkspaceSessionSnapshot session)
    {
        bool invalidateQueryCache;
        lock (_syncRoot)
        {
            if (_snapshot.TransactionOwnerWorkspaceId != session.Workspace.WorkspaceId)
            {
                return TransactionCompletionResult.OwnershipChanged(
                    session.Workspace.WorkspaceId,
                    _snapshot.TransactionOwnerWorkspaceId);
            }

            _snapshot.Workspaces.TryGetValue(session.Workspace.WorkspaceId, out var previousSession);
            NotifySnapshotLifecycle(previousSession, session, []);
            invalidateQueryCache = ReplaceSessionLocked(session);
            _snapshot = _snapshot with
            {
                TransactionOwnerWorkspaceId = null,
            };
        }

        if (invalidateQueryCache)
        {
            _queryCache.InvalidateWorkspace(session.Workspace.WorkspaceId);
        }

        return TransactionCompletionResult.Completed();
    }

    private void ReplaceSessionCore(
        WorkspaceSessionSnapshot session,
        IReadOnlyList<WorkspaceSnapshotId> discardedSnapshotIds)
    {
        bool invalidateQueryCache;
        lock (_syncRoot)
        {
            _snapshot.Workspaces.TryGetValue(session.Workspace.WorkspaceId, out var previousSession);
            NotifySnapshotLifecycle(previousSession, session, discardedSnapshotIds);
            invalidateQueryCache = ReplaceSessionLocked(session);
        }

        if (invalidateQueryCache)
        {
            _queryCache.InvalidateWorkspace(session.Workspace.WorkspaceId);
        }
    }

    private bool ReplaceSessionLocked(WorkspaceSessionSnapshot session)
    {
        var invalidateQueryCache = !_snapshot.Workspaces.TryGetValue(session.Workspace.WorkspaceId, out var previousSession)
            || !ReferenceEquals(previousSession.CurrentSolution, session.CurrentSolution)
            || previousSession.Workspace.WorkspaceEpoch != session.Workspace.WorkspaceEpoch
            || session.State is WorkspaceLifecycleState.WorkspaceOutOfDate
                or WorkspaceLifecycleState.TransactionConflicted;

        var workspaces = new Dictionary<Guid, WorkspaceSessionSnapshot>(_snapshot.Workspaces)
        {
            [session.Workspace.WorkspaceId] = session,
        };

        _snapshot = _snapshot with
        {
            Workspaces = workspaces,
        };

        return invalidateQueryCache;
    }

    private void NotifySnapshotLifecycle(
        WorkspaceSessionSnapshot? previousSession,
        WorkspaceSessionSnapshot currentSession,
        IReadOnlyList<WorkspaceSnapshotId> discardedSnapshotIds)
    {
        if (previousSession is null)
        {
            return;
        }

        if (previousSession.Workspace.WorkspaceEpoch != currentSession.Workspace.WorkspaceEpoch)
        {
            InvalidateWorkspace(previousSession);
            return;
        }

        if (previousSession.State != currentSession.State
            && currentSession.State is WorkspaceLifecycleState.WorkspaceOutOfDate
                or WorkspaceLifecycleState.TransactionConflicted)
        {
            InvalidateCurrentScope(previousSession);
            return;
        }

        var previousTransaction = previousSession.Transaction;
        var currentTransaction = currentSession.Transaction;

        if (previousTransaction is null)
        {
            if (currentTransaction is not null
                || previousSession.CommittedSnapshotId != currentSession.CommittedSnapshotId)
            {
                InvalidateSnapshots([previousSession.CurrentSnapshotIdentity]);
            }

            return;
        }

        if (currentTransaction is null
            || previousTransaction.TransactionId != currentTransaction.TransactionId)
        {
            InvalidateTransaction(previousSession, previousTransaction.TransactionId);
            return;
        }

        if (discardedSnapshotIds.Count == 0)
        {
            return;
        }

        var discardedSnapshots = discardedSnapshotIds
            .Select(snapshotId => new WorkspaceSnapshotIdentity(
                previousSession.Workspace.WorkspaceId,
                previousSession.Workspace.WorkspaceEpoch,
                snapshotId,
                previousTransaction.TransactionId))
            .ToArray();

        InvalidateSnapshots(discardedSnapshots);
    }

    private void InvalidateCurrentScope(WorkspaceSessionSnapshot session)
    {
        if (session.Transaction is null)
        {
            InvalidateSnapshots([session.CurrentSnapshotIdentity]);
            return;
        }

        InvalidateTransaction(session, session.Transaction.TransactionId);
    }

    private void InvalidateWorkspace(WorkspaceSessionSnapshot session)
    {
        foreach (var observer in _lifecycleObservers)
        {
            observer.InvalidateWorkspace(
                session.Workspace.WorkspaceId,
                session.Workspace.WorkspaceEpoch);
        }
    }

    private void InvalidateTransaction(
        WorkspaceSessionSnapshot session,
        WorkspaceTransactionId transactionId)
    {
        foreach (var observer in _lifecycleObservers)
        {
            observer.InvalidateTransaction(
                session.Workspace.WorkspaceId,
                session.Workspace.WorkspaceEpoch,
                transactionId);
        }
    }

    private void InvalidateSnapshots(IReadOnlyList<WorkspaceSnapshotIdentity> snapshots)
    {
        foreach (var observer in _lifecycleObservers)
        {
            observer.InvalidateSnapshots(snapshots);
        }
    }
}
