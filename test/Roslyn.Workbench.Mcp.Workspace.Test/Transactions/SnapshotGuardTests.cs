namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class SnapshotGuardTests : IDisposable
{
    private readonly AdhocWorkspace _workspace;
    private readonly SnapshotGuard _target;

    public SnapshotGuardTests()
    {
        _workspace = new AdhocWorkspace();
        _target = new SnapshotGuard();
    }

    [Fact]
    public void GIVEN_NullSession_WHEN_Validating_THEN_ShouldThrowArgumentNullException()
    {
        var action = () => _target.Validate(null!, expectedSnapshot: null);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GIVEN_SessionWithoutTransaction_WHEN_Validating_THEN_ShouldReturnNoError()
    {
        var session = CreateSession(transaction: null);
        var expectedSnapshot = CreateExpectedSnapshot();

        var result = _target.Validate(session, expectedSnapshot);

        result.Should().BeNull();
    }

    [Fact]
    public void GIVEN_NoExpectedSnapshot_WHEN_Validating_THEN_ShouldReturnNoError()
    {
        var session = CreateSession(CreateTransaction());

        var result = _target.Validate(session, expectedSnapshot: null);

        result.Should().BeNull();
    }

    [Fact]
    public void GIVEN_DifferentWorkspaceId_WHEN_Validating_THEN_ShouldReturnSnapshotMismatch()
    {
        var session = CreateSession(CreateTransaction());
        var expectedSnapshot = CreateExpectedSnapshot() with
        {
            WorkspaceId = "DifferentWorkspaceId",
        };

        var result = _target.Validate(session, expectedSnapshot);

        result!.Code.Should().Be("SnapshotMismatch");
        result.Message.Should().Be("The request snapshot does not match the current transaction snapshot.");
        result.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public void GIVEN_DifferentWorkspaceEpoch_WHEN_Validating_THEN_ShouldReturnSnapshotMismatch()
    {
        var session = CreateSession(CreateTransaction());
        var expectedSnapshot = CreateExpectedSnapshot() with
        {
            WorkspaceEpoch = 2,
        };

        var result = _target.Validate(session, expectedSnapshot);

        result!.Code.Should().Be("SnapshotMismatch");
        result.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public void GIVEN_DifferentTransactionRevision_WHEN_Validating_THEN_ShouldReturnSnapshotMismatch()
    {
        var session = CreateSession(CreateTransaction());
        var expectedSnapshot = CreateExpectedSnapshot() with
        {
            TransactionRevision = 2,
        };

        var result = _target.Validate(session, expectedSnapshot);

        result!.Code.Should().Be("SnapshotMismatch");
        result.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public void GIVEN_BlankOptionalWorkspaceIdAndMatchingEpochAndRevision_WHEN_Validating_THEN_ShouldReturnNoError()
    {
        var session = CreateSession(CreateTransaction());
        var expectedSnapshot = CreateExpectedSnapshot() with
        {
            WorkspaceId = "   ",
        };

        var result = _target.Validate(session, expectedSnapshot);

        result.Should().BeNull();
    }

    [Fact]
    public void GIVEN_ExactSnapshot_WHEN_Validating_THEN_ShouldReturnNoError()
    {
        var session = CreateSession(CreateTransaction());
        var expectedSnapshot = CreateExpectedSnapshot();

        var result = _target.Validate(session, expectedSnapshot);

        result.Should().BeNull();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    private WorkspaceSessionSnapshot CreateSession(WorkspaceTransaction? transaction)
    {
        return new WorkspaceSessionSnapshot
        {
            State = WorkspaceLifecycleState.TransactionActive,
            Workspace = new WorkspaceIdentity
            {
                WorkspaceId = "WorkspaceId",
                WorkspaceEpoch = 1,
                LoadedPath = "LoadedPath",
            },
            LoadedWorkspace = null!,
            CurrentSolution = _workspace.CurrentSolution,
            Transaction = transaction,
            InputManifest = null!,
            OperationGate = null!,
        };
    }

    private WorkspaceTransaction CreateTransaction()
    {
        return new WorkspaceTransaction
        {
            BaselineSolution = _workspace.CurrentSolution,
            CurrentRevision = 1,
        };
    }

    private static SnapshotPrecondition CreateExpectedSnapshot()
    {
        return new SnapshotPrecondition
        {
            WorkspaceId = "WorkspaceId",
            WorkspaceEpoch = 1,
            TransactionRevision = 1,
        };
    }
}
