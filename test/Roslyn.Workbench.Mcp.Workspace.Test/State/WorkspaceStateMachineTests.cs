namespace Roslyn.Workbench.Mcp.Workspace.Test.State;

public sealed class WorkspaceStateMachineTests
{
    [Fact]
    public async Task GIVEN_ReadyState_WHEN_GettingPermittedTriggers_THEN_ShouldReturnExternalChangeAndTransactionStart()
    {
        var result = await WorkspaceStateMachine.GetPermittedTriggersAsync(WorkspaceLifecycleState.Ready);

        result.Should().BeEquivalentTo(
        [
            WorkspaceTrigger.ExternalChangeDetected,
            WorkspaceTrigger.TransactionStarted,
        ]);
    }

    [Fact]
    public async Task GIVEN_TransactionActiveState_WHEN_GettingPermittedTriggers_THEN_ShouldReturnCompletionAndConflictTriggers()
    {
        var result = await WorkspaceStateMachine.GetPermittedTriggersAsync(WorkspaceLifecycleState.TransactionActive);

        result.Should().BeEquivalentTo(
        [
            WorkspaceTrigger.TransactionCommitted,
            WorkspaceTrigger.TransactionRolledBack,
            WorkspaceTrigger.TransactionConflictDetected,
        ]);
    }

    [Fact]
    public async Task GIVEN_TransactionConflictedState_WHEN_GettingPermittedTriggers_THEN_ShouldReturnConflictedRollbackTrigger()
    {
        var result = await WorkspaceStateMachine.GetPermittedTriggersAsync(WorkspaceLifecycleState.TransactionConflicted);

        result.Should().ContainSingle().Which.Should().Be(WorkspaceTrigger.ConflictedRollbackCompleted);
    }

    [Fact]
    public async Task GIVEN_WorkspaceOutOfDateState_WHEN_GettingPermittedTriggers_THEN_ShouldReturnReloadTrigger()
    {
        var result = await WorkspaceStateMachine.GetPermittedTriggersAsync(WorkspaceLifecycleState.WorkspaceOutOfDate);

        result.Should().ContainSingle().Which.Should().Be(WorkspaceTrigger.ReloadSucceeded);
    }

    [Theory]
    [InlineData("Ready", "ExternalChangeDetected", "WorkspaceOutOfDate")]
    [InlineData("Ready", "TransactionStarted", "TransactionActive")]
    [InlineData("TransactionActive", "TransactionCommitted", "Ready")]
    [InlineData("TransactionActive", "TransactionRolledBack", "Ready")]
    [InlineData("TransactionActive", "TransactionConflictDetected", "TransactionConflicted")]
    [InlineData("TransactionConflicted", "ConflictedRollbackCompleted", "WorkspaceOutOfDate")]
    [InlineData("WorkspaceOutOfDate", "ReloadSucceeded", "Ready")]
    public void GIVEN_PermittedTrigger_WHEN_Firing_THEN_ShouldReturnConfiguredState(
        string stateName,
        string triggerName,
        string expectedStateName)
    {
        var state = Enum.Parse<WorkspaceLifecycleState>(stateName);
        var trigger = Enum.Parse<WorkspaceTrigger>(triggerName);
        var expectedState = Enum.Parse<WorkspaceLifecycleState>(expectedStateName);

        var result = WorkspaceStateMachine.Fire(state, trigger);

        result.Should().Be(expectedState);
    }

    [Fact]
    public void GIVEN_InvalidTriggerForState_WHEN_Firing_THEN_ShouldThrowAndLeaveStateUnchanged()
    {
        var state = WorkspaceLifecycleState.Ready;
        var machine = WorkspaceStateMachine.Create(
            () => state,
            value => state = value);

        var action = () => machine.Fire(WorkspaceTrigger.TransactionCommitted);

        action.Should().Throw<InvalidOperationException>();
        state.Should().Be(WorkspaceLifecycleState.Ready);
    }
}
