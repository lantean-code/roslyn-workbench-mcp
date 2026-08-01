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
            WorkspaceId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
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
    public void GIVEN_BlankWorkspaceIdAndMatchingEpochAndRevision_WHEN_Validating_THEN_ShouldReturnSnapshotMismatch()
    {
        var session = CreateSession(CreateTransaction());
        var expectedSnapshot = CreateExpectedSnapshot() with
        {
            WorkspaceId = Guid.Empty,
        };

        var result = _target.Validate(session, expectedSnapshot);

        result!.Code.Should().Be("SnapshotMismatch");
        result.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
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
        var committedSnapshotId = new WorkspaceSnapshotId(1);
        var state = WorkspaceLifecycleState.Ready;
        if (transaction is not null)
        {
            state = WorkspaceLifecycleState.TransactionActive;
        }

        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            WorkspaceEpoch = 1,
            LoadedPath = "LoadedPath",
        };

        return new WorkspaceSessionSnapshot
        {
            CommittedSnapshotId = committedSnapshotId,
            State = state,
            Workspace = workspaceIdentity,
            LoadedWorkspace = null!,
            CurrentSolution = _workspace.CurrentSolution,
            Transaction = transaction,
            InputManifest = null!,
            OperationGate = null!,
            CurrentSnapshotIdentity = WorkspaceSnapshotIdentity.Create(
                workspaceIdentity,
                committedSnapshotId,
                transaction),
        };
    }

    private WorkspaceTransaction CreateTransaction()
    {
        var revision = new WorkspaceTransactionRevision
        {
            SnapshotId = new WorkspaceSnapshotId(2),
            Solution = _workspace.CurrentSolution,
            Changes = new ChangeSummary(),
            Operation = "Operation",
            Summary = "Summary",
            Preview = new MutationPreview(),
        };

        return new WorkspaceTransaction
        {
            TransactionId = new WorkspaceTransactionId(1),
            BaselineSnapshotId = new WorkspaceSnapshotId(1),
            BaselineSolution = _workspace.CurrentSolution,
            Revisions = [revision],
            CurrentRevision = 1,
        };
    }

    private static SnapshotPrecondition CreateExpectedSnapshot()
    {
        return new SnapshotPrecondition
        {
            WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            WorkspaceEpoch = 1,
            TransactionRevision = 1,
        };
    }
}
