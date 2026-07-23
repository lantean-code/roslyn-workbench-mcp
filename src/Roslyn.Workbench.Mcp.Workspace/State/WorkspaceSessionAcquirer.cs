namespace Roslyn.Workbench.Mcp.Workspace.State;

internal sealed class WorkspaceSessionAcquirer : IWorkspaceSessionAcquirer
{
    private readonly IWorkspaceSessionStore _sessionStore;
    private readonly IWorkspaceSelector _workspaceSelector;

    public WorkspaceSessionAcquirer(IWorkspaceSessionStore sessionStore, IWorkspaceSelector workspaceSelector)
    {
        _sessionStore = sessionStore;
        _workspaceSelector = workspaceSelector;
    }

    public WorkspaceSessionAcquisition AcquireShared(WorkspaceSelector? selector)
    {
        return Acquire(selector, requiresExclusiveAccess: false);
    }

    public WorkspaceSessionAcquisition AcquireExclusive(WorkspaceSelector? selector)
    {
        return Acquire(selector, requiresExclusiveAccess: true);
    }

    private WorkspaceSessionAcquisition Acquire(WorkspaceSelector? selector, bool requiresExclusiveAccess)
    {
        var hostSnapshot = _sessionStore.ReadSnapshot();
        if (hostSnapshot.Workspaces.Count == 0)
        {
            return WorkspaceSessionAcquisition.Rejected(CreateWorkspaceRequiredError());
        }

        var selectionResult = _workspaceSelector.Select(hostSnapshot, selector);
        if (selectionResult.HasError)
        {
            return WorkspaceSessionAcquisition.Rejected(selectionResult.Error);
        }

        var selection = selectionResult.Selection;
        IWorkspaceOperationLease? lease;
        if (requiresExclusiveAccess)
        {
            lease = selection.Session.OperationGate.TryAcquireExclusive();
        }
        else
        {
            lease = selection.Session.OperationGate.TryAcquireShared();
        }

        if (lease is null)
        {
            return WorkspaceSessionAcquisition.Rejected(CreateBusyError(), selection.Session);
        }

        var session = _sessionStore.ReadSession(selection.WorkspaceId);
        if (session is null)
        {
            return WorkspaceSessionAcquisition.Rejected(CreateWorkspaceRequiredError(), lease: lease);
        }

        var updatedSelection = selection with { Session = session };

        return WorkspaceSessionAcquisition.Acquired(updatedSelection, session, lease);
    }

    private static WorkspaceOperationError CreateBusyError()
    {
        return new WorkspaceOperationError
        {
            Code = WorkspaceErrorCodes.WorkspaceBusy,
            Message = "The workspace is busy.",
            RequiredAction = RequiredAction.Retry,
        };
    }

    private static WorkspaceOperationError CreateWorkspaceRequiredError()
    {
        return new WorkspaceOperationError
        {
            Code = WorkspaceErrorCodes.WorkspaceNotOpen,
            Message = "Open a workspace before invoking this tool.",
            RequiredAction = RequiredAction.OpenWorkspace,
        };
    }
}
