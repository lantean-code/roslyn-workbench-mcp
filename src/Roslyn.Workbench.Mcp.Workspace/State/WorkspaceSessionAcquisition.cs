using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.State;

internal sealed class WorkspaceSessionAcquisition
{
    private WorkspaceSessionAcquisition(
        WorkspaceSelection? selection,
        WorkspaceSessionSnapshot? session,
        WorkspaceSessionSnapshot? contextSession,
        IWorkspaceOperationLease? lease,
        WorkspaceOperationError? error)
    {
        Selection = selection;
        Session = session;
        ContextSession = contextSession;
        Lease = lease;
        Error = error;
    }

    public WorkspaceSessionSnapshot? ContextSession { get; }

    public WorkspaceOperationError? Error { get; }

    public IWorkspaceOperationLease? Lease { get; }

    public WorkspaceSelection? Selection { get; }

    public WorkspaceSessionSnapshot? Session { get; }

    [MemberNotNullWhen(true, nameof(Error))]
    [MemberNotNullWhen(false, nameof(Lease))]
    [MemberNotNullWhen(false, nameof(Selection))]
    [MemberNotNullWhen(false, nameof(Session))]
    public bool HasError => Error is not null;

    public static WorkspaceSessionAcquisition Acquired(
        WorkspaceSelection selection,
        WorkspaceSessionSnapshot session,
        IWorkspaceOperationLease lease)
    {
        return new WorkspaceSessionAcquisition(
            selection,
            session,
            session,
            lease,
            error: null);
    }

    public static WorkspaceSessionAcquisition Rejected(
        WorkspaceOperationError error,
        WorkspaceSessionSnapshot? contextSession = null,
        IWorkspaceOperationLease? lease = null)
    {
        return new WorkspaceSessionAcquisition(
            selection: null,
            session: null,
            contextSession,
            lease,
            error);
    }
}
