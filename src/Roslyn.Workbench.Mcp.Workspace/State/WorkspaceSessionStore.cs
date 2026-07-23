using Roslyn.Workbench.Mcp.Workspace.Caching;

namespace Roslyn.Workbench.Mcp.Workspace.State;

internal sealed class WorkspaceSessionStore : IWorkspaceSessionStore
{
    private readonly IWorkspaceQueryCache _queryCache;
    private readonly Lock _syncRoot;
    private WorkspaceHostSnapshot _snapshot;
    private long _nextWorkspaceEpoch;
    private long _nextWorkspaceId;

    public WorkspaceSessionStore(IWorkspaceQueryCache queryCache)
    {
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

    public WorkspaceSessionSnapshot? ReadSession(string workspaceId)
    {
        lock (_syncRoot)
        {
            return _snapshot.Workspaces.TryGetValue(workspaceId, out var session)
                ? session
                : null;
        }
    }

    public string AllocateWorkspaceId()
    {
        var nextValue = Interlocked.Increment(ref _nextWorkspaceId);
        return $"workspace-{nextValue}";
    }

    public long AllocateWorkspaceEpoch()
    {
        return Interlocked.Increment(ref _nextWorkspaceEpoch);
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

            var workspaces = new Dictionary<string, WorkspaceSessionSnapshot>(_snapshot.Workspaces, StringComparer.Ordinal)
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

    public WorkspaceSessionSnapshot? RemoveWorkspace(string workspaceId)
    {
        WorkspaceSessionSnapshot? session;
        lock (_syncRoot)
        {
            if (!_snapshot.Workspaces.TryGetValue(workspaceId, out session))
            {
                return null;
            }

            var workspaces = new Dictionary<string, WorkspaceSessionSnapshot>(_snapshot.Workspaces, StringComparer.Ordinal);
            workspaces.Remove(workspaceId);
            _snapshot = _snapshot with
            {
                Workspaces = workspaces,
                TransactionOwnerWorkspaceId = string.Equals(_snapshot.TransactionOwnerWorkspaceId, workspaceId, StringComparison.Ordinal)
                    ? null
                    : _snapshot.TransactionOwnerWorkspaceId,
            };
        }

        _queryCache.InvalidateWorkspace(workspaceId);
        return session;
    }

    public void ReplaceSession(WorkspaceSessionSnapshot session)
    {
        bool invalidateQueryCache;
        lock (_syncRoot)
        {
            invalidateQueryCache = ReplaceSessionLocked(session);
        }

        if (invalidateQueryCache)
        {
            _queryCache.InvalidateWorkspace(session.Workspace.WorkspaceId);
        }
    }

    public void ReplaceSessionAndSetTransactionOwner(WorkspaceSessionSnapshot session, string? transactionOwnerWorkspaceId)
    {
        bool invalidateQueryCache;
        lock (_syncRoot)
        {
            invalidateQueryCache = ReplaceSessionLocked(session);
            _snapshot = _snapshot with
            {
                TransactionOwnerWorkspaceId = transactionOwnerWorkspaceId,
            };
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
            || session.State == WorkspaceLifecycleState.WorkspaceOutOfDate;

        var workspaces = new Dictionary<string, WorkspaceSessionSnapshot>(_snapshot.Workspaces, StringComparer.Ordinal)
        {
            [session.Workspace.WorkspaceId] = session,
        };

        _snapshot = _snapshot with
        {
            Workspaces = workspaces,
        };

        return invalidateQueryCache;
    }
}
