using Roslyn.Workbench.Mcp.Contracts.Server;

using Stateless;

namespace Roslyn.Workbench.Mcp.Workspace;

internal static class WorkspaceStateMachine
{
    public static StateMachine<WorkspaceLifecycleState, WorkspaceTrigger> Create(
        Func<WorkspaceLifecycleState> stateAccessor,
        Action<WorkspaceLifecycleState> stateMutator)
    {
        var machine = new StateMachine<WorkspaceLifecycleState, WorkspaceTrigger>(stateAccessor, stateMutator);

        machine.Configure(WorkspaceLifecycleState.Unloaded)
            .Permit(WorkspaceTrigger.OpenSucceeded, WorkspaceLifecycleState.Ready);

        machine.Configure(WorkspaceLifecycleState.Ready)
            .Permit(WorkspaceTrigger.CloseSucceeded, WorkspaceLifecycleState.Unloaded)
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

    public static async Task<IReadOnlyList<WorkspaceTrigger>> GetPermittedTriggersAsync(WorkspaceLifecycleState state)
    {
        var currentState = state;
        var machine = Create(() => currentState, value => currentState = value);

        return (await machine.GetPermittedTriggersAsync()).ToList().AsReadOnly();
    }

    public static WorkspaceLifecycleState Fire(WorkspaceLifecycleState state, WorkspaceTrigger trigger)
    {
        var currentState = state;
        var machine = Create(() => currentState, value => currentState = value);

        machine.Fire(trigger);
        return currentState;
    }
}
