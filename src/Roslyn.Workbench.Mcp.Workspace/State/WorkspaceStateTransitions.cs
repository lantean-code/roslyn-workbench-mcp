
namespace Roslyn.Workbench.Mcp.Workspace.State;

internal sealed class WorkspaceStateTransitions : IWorkspaceStateTransitions
{
    public WorkspaceLifecycleState Fire(WorkspaceLifecycleState state, WorkspaceTrigger trigger)
    {
        return WorkspaceStateMachine.Fire(state, trigger);
    }

    public WorkspaceSessionSnapshot ApplyExternalChangeDetected(WorkspaceSessionSnapshot session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var trigger = session.State switch
        {
            WorkspaceLifecycleState.Ready => WorkspaceTrigger.ExternalChangeDetected,
            WorkspaceLifecycleState.TransactionActive => WorkspaceTrigger.TransactionConflictDetected,
            _ => (WorkspaceTrigger?)null,
        };

        if (trigger is null)
        {
            return session;
        }

        return session with
        {
            State = Fire(session.State, trigger.Value),
        };
    }
}
