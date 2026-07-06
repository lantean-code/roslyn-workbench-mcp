namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed class WorkspaceSessionStore : IWorkspaceSessionStore
{
    private readonly Lock _syncRoot;
    private WorkspaceHostSnapshot _snapshot;
    private long _nextWorkspaceEpoch;
    private long _nextWorkspaceId;

    public WorkspaceSessionStore()
    {
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
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(validate);

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
        lock (_syncRoot)
        {
            if (!_snapshot.Workspaces.TryGetValue(workspaceId, out var session))
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

            return session;
        }
    }

    public void ReplaceSession(WorkspaceSessionSnapshot session)
    {
        ArgumentNullException.ThrowIfNull(session);

        lock (_syncRoot)
        {
            ReplaceSessionLocked(session);
        }
    }

    public void ReplaceSessionAndSetTransactionOwner(WorkspaceSessionSnapshot session, string? transactionOwnerWorkspaceId)
    {
        ArgumentNullException.ThrowIfNull(session);

        lock (_syncRoot)
        {
            ReplaceSessionLocked(session);
            _snapshot = _snapshot with
            {
                TransactionOwnerWorkspaceId = transactionOwnerWorkspaceId,
            };
        }
    }

    private void ReplaceSessionLocked(WorkspaceSessionSnapshot session)
    {
        var workspaces = new Dictionary<string, WorkspaceSessionSnapshot>(_snapshot.Workspaces, StringComparer.Ordinal)
        {
            [session.Workspace.WorkspaceId] = session,
        };
        _snapshot = _snapshot with
        {
            Workspaces = workspaces,
        };
    }
}
