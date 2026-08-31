namespace Roslyn.Workbench.Mcp.Workspace.State;

/// <summary>
/// Defines the permitted Workspace lifecycle graph independently of session storage.
/// </summary>
internal static class WorkspaceStateMachine
{
    /// <summary>
    /// Creates a state machine bound to caller-owned state accessors.
    /// </summary>
    /// <param name="stateAccessor">Reads the current lifecycle state.</param>
    /// <param name="stateMutator">Stores a transitioned lifecycle state.</param>
    /// <returns>The configured Workspace lifecycle state machine.</returns>
    public static StateMachine<WorkspaceLifecycleState, WorkspaceTrigger> Create(
        Func<WorkspaceLifecycleState> stateAccessor,
        Action<WorkspaceLifecycleState> stateMutator)
    {
        var machine = new StateMachine<WorkspaceLifecycleState, WorkspaceTrigger>(stateAccessor, stateMutator);

        machine.Configure(WorkspaceLifecycleState.Ready)
            .Permit(WorkspaceTrigger.ExternalChangeDetected, WorkspaceLifecycleState.WorkspaceOutOfDate)
            .Permit(WorkspaceTrigger.TransactionStarted, WorkspaceLifecycleState.TransactionActive);

        machine.Configure(WorkspaceLifecycleState.TransactionActive)
            .Permit(WorkspaceTrigger.TransactionCommitted, WorkspaceLifecycleState.Ready)
            .Permit(WorkspaceTrigger.TransactionRolledBack, WorkspaceLifecycleState.Ready)
            .Permit(WorkspaceTrigger.TransactionConflictDetected, WorkspaceLifecycleState.TransactionConflicted);

        machine.Configure(WorkspaceLifecycleState.TransactionConflicted)
            .Permit(WorkspaceTrigger.ConflictedRollbackCompleted, WorkspaceLifecycleState.WorkspaceOutOfDate);

        machine.Configure(WorkspaceLifecycleState.WorkspaceOutOfDate)
            .Permit(WorkspaceTrigger.ReloadSucceeded, WorkspaceLifecycleState.Ready);

        return machine;
    }

    /// <summary>
    /// Gets the triggers permitted from a lifecycle state without mutating a session.
    /// </summary>
    /// <param name="state">The lifecycle state to inspect.</param>
    /// <returns>The permitted triggers.</returns>
    public static async Task<IReadOnlyList<WorkspaceTrigger>> GetPermittedTriggersAsync(WorkspaceLifecycleState state)
    {
        var currentState = state;
        var machine = Create(() => currentState, value => currentState = value);

        return (await machine.GetPermittedTriggersAsync()).ToList().AsReadOnly();
    }

    /// <summary>
    /// Applies one permitted trigger to a lifecycle state.
    /// </summary>
    /// <param name="state">The current lifecycle state.</param>
    /// <param name="trigger">The trigger to fire.</param>
    /// <returns>The resulting lifecycle state.</returns>
    public static WorkspaceLifecycleState Fire(WorkspaceLifecycleState state, WorkspaceTrigger trigger)
    {
        var currentState = state;
        var machine = Create(() => currentState, value => currentState = value);

        machine.Fire(trigger);
        return currentState;
    }
}
