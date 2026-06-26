using AwesomeAssertions;

using Roslyn.Workbench.Mcp.Contracts.Server;

using Roslyn.Workbench.Mcp.Workspace;

using Xunit;

namespace Roslyn.Workbench.Mcp.Workspace.Test;

public sealed class WorkspaceStateMachineTests
{
    [Fact]
    public async Task GIVEN_ReadyState_WHEN_InspectingPermittedTriggers_THEN_ShouldExposeOnlyExpectedTransitions()
    {
        var triggers = await WorkspaceStateMachine.GetPermittedTriggersAsync(WorkspaceLifecycleState.Ready);

        triggers.Should().BeEquivalentTo(
        [
            WorkspaceTrigger.CloseSucceeded,
            WorkspaceTrigger.ExternalChangeDetected,
            WorkspaceTrigger.TransactionStarted,
        ]);
    }

    [Fact]
    public async Task GIVEN_UnloadedState_WHEN_InspectingPermittedTriggers_THEN_ShouldNotAllowTransactionTransitions()
    {
        var triggers = await WorkspaceStateMachine.GetPermittedTriggersAsync(WorkspaceLifecycleState.Unloaded);

        triggers.Should().ContainSingle(static trigger => trigger == WorkspaceTrigger.OpenSucceeded);
        triggers.Should().NotContain(WorkspaceTrigger.TransactionStarted);
        triggers.Should().NotContain(WorkspaceTrigger.TransactionConflictDetected);
    }
}
