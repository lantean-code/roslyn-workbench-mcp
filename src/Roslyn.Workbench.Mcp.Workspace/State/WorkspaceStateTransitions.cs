namespace Roslyn.Workbench.Mcp.Workspace.State;

/// <summary>
/// Applies lifecycle state-machine transitions to immutable Workspace session snapshots.
/// </summary>
internal sealed class WorkspaceStateTransitions : IWorkspaceStateTransitions
{
    /// <inheritdoc/>
    public WorkspaceLifecycleState Fire(WorkspaceLifecycleState state, WorkspaceTrigger trigger)
    {
        return WorkspaceStateMachine.Fire(state, trigger);
    }

    /// <inheritdoc/>
    public WorkspaceSessionSnapshot ApplyExternalChangeDetected(WorkspaceSessionSnapshot session)
    {
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
