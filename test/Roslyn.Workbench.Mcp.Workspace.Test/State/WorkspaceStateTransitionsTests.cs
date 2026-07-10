namespace Roslyn.Workbench.Mcp.Workspace.Test.State;

public sealed class WorkspaceStateTransitionsTests
{
    private readonly WorkspaceStateTransitions _target;

    public WorkspaceStateTransitionsTests()
    {
        _target = new WorkspaceStateTransitions();
    }

    [Fact]
    public void GIVEN_ValidTransition_WHEN_Firing_THEN_ShouldDelegateToStateMachine()
    {
        var result = _target.Fire(WorkspaceLifecycleState.Ready, WorkspaceTrigger.TransactionStarted);

        result.Should().Be(WorkspaceLifecycleState.TransactionActive);
    }

    [Fact]
    public void GIVEN_NullSession_WHEN_ApplyingExternalChange_THEN_ShouldThrowArgumentNullException()
    {
        var action = () => _target.ApplyExternalChangeDetected(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GIVEN_ReadySession_WHEN_ApplyingExternalChange_THEN_ShouldReturnOutOfDateCopy()
    {
        var session = CreateSession(WorkspaceLifecycleState.Ready);

        var result = _target.ApplyExternalChangeDetected(session);

        result.Should().NotBeSameAs(session);
        result.State.Should().Be(WorkspaceLifecycleState.WorkspaceOutOfDate);
    }

    [Fact]
    public void GIVEN_ActiveTransaction_WHEN_ApplyingExternalChange_THEN_ShouldReturnConflictedCopy()
    {
        var session = CreateSession(WorkspaceLifecycleState.TransactionActive);

        var result = _target.ApplyExternalChangeDetected(session);

        result.Should().NotBeSameAs(session);
        result.State.Should().Be(WorkspaceLifecycleState.TransactionConflicted);
    }

    [Theory]
    [InlineData(WorkspaceLifecycleState.TransactionConflicted)]
    [InlineData(WorkspaceLifecycleState.WorkspaceOutOfDate)]
    public void GIVEN_UnsupportedSessionState_WHEN_ApplyingExternalChange_THEN_ShouldReturnSameSession(WorkspaceLifecycleState state)
    {
        var session = CreateSession(state);

        var result = _target.ApplyExternalChangeDetected(session);

        result.Should().BeSameAs(session);
    }

    private static WorkspaceSessionSnapshot CreateSession(WorkspaceLifecycleState state)
    {
        return new WorkspaceSessionSnapshot
        {
            State = state,
            Workspace = new WorkspaceIdentity
            {
                WorkspaceId = "WorkspaceId",
                LoadedPath = "LoadedPath",
            },
            LoadedWorkspace = null!,
            CurrentSolution = null!,
            InputManifest = null!,
            OperationGate = null!,
        };
    }
}
