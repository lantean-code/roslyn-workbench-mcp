namespace Roslyn.Workbench.Mcp.Workspace.Test;

public sealed class WorkspaceStateMachineTests
{
    [Fact]
    public async Task GIVEN_ReadyState_WHEN_InspectingPermittedTriggers_THEN_ShouldExposeOnlyExpectedTransitions()
    {
        var triggers = await WorkspaceStateMachine.GetPermittedTriggersAsync(WorkspaceLifecycleState.Ready);

        triggers.Should().BeEquivalentTo(
        [
            WorkspaceTrigger.ExternalChangeDetected,
            WorkspaceTrigger.TransactionStarted,
        ]);
    }
}
