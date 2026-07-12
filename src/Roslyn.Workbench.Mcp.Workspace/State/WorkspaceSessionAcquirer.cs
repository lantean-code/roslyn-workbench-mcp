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
        var lease = requiresExclusiveAccess
            ? selection.Session.OperationGate.TryAcquireExclusive()
            : selection.Session.OperationGate.TryAcquireShared();
        if (lease is null)
        {
            return WorkspaceSessionAcquisition.Rejected(CreateBusyError(), selection.Session);
        }

        var session = _sessionStore.ReadSession(selection.WorkspaceId);
        return session is null
            ? WorkspaceSessionAcquisition.Rejected(CreateWorkspaceRequiredError(), lease: lease)
            : WorkspaceSessionAcquisition.Acquired(selection with { Session = session }, session, lease);
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
