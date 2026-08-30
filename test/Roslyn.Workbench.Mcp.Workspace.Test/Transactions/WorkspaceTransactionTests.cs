namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class WorkspaceTransactionTests : IDisposable
{
    private readonly AdhocWorkspace _workspace = new();

    [Fact]
    public void GIVEN_TransactionAtEarlierRevision_WHEN_AppendingRevision_THEN_ShouldDiscardRedoHistory()
    {
        var baselineSolution = _workspace.CurrentSolution;
        var firstSolution = baselineSolution.AddProject("First", "First", LanguageNames.CSharp).Solution;
        var redoSolution = firstSolution.AddProject("Redo", "Redo", LanguageNames.CSharp).Solution;
        var appendedSolution = firstSolution.AddProject("Appended", "Appended", LanguageNames.CSharp).Solution;
        var appendedRevision = CreateRevision(appendedSolution, 3);
        var target = new WorkspaceTransaction
        {
            TransactionId = new WorkspaceTransactionId(1),
            BaselineSnapshotId = WorkspaceSnapshotTestFactory.CreateId(1),
            BaselineSolution = baselineSolution,
            Revisions =
            [
                CreateRevision(firstSolution, 1),
                CreateRevision(redoSolution, 2),
            ],
            CurrentRevision = 1,
            MaxRevisions = 3,
        };

        var result = target.Append(appendedRevision);

        result.Transaction.CurrentRevision.Should().Be(2);
        result.Transaction.Revisions.Should().Equal(target.Revisions[0], appendedRevision);
        result.Transaction.CurrentSolution.Should().BeSameAs(appendedSolution);
        result.DiscardedSnapshotIds.Should().Equal(WorkspaceSnapshotTestFactory.CreateId(2));
    }

    [Theory]
    [InlineData(TransactionHistoryDirection.Undo, 1, 0)]
    [InlineData(TransactionHistoryDirection.Redo, 0, 1)]
    [InlineData(TransactionHistoryDirection.Undo, 0, null)]
    [InlineData(TransactionHistoryDirection.Redo, 1, null)]
    public void GIVEN_TransactionHistory_WHEN_Moving_THEN_ShouldReturnExpectedRevision(
        TransactionHistoryDirection direction,
        int currentRevision,
        int? expectedRevision)
    {
        var target = new WorkspaceTransaction
        {
            TransactionId = new WorkspaceTransactionId(1),
            BaselineSnapshotId = WorkspaceSnapshotTestFactory.CreateId(1),
            BaselineSolution = _workspace.CurrentSolution,
            Revisions = [CreateRevision(_workspace.CurrentSolution)],
            CurrentRevision = currentRevision,
            MaxRevisions = 3,
        };

        var result = target.MoveHistory(direction);

        if (expectedRevision is null)
        {
            result.Should().BeNull();
        }
        else
        {
            result.Should().NotBeNull();
            result.CurrentRevision.Should().Be(expectedRevision.Value);
            target.CurrentRevision.Should().Be(currentRevision);
        }
    }

    [Fact]
    public void GIVEN_ZeroCurrentRevision_WHEN_GettingCurrentSolution_THEN_ShouldReturnBaselineSolution()
    {
        using var workspace = new AdhocWorkspace();
        var baselineSolution = workspace.CurrentSolution;
        var target = new WorkspaceTransaction
        {
            TransactionId = new WorkspaceTransactionId(1),
            BaselineSnapshotId = WorkspaceSnapshotTestFactory.CreateId(1),
            BaselineSolution = baselineSolution,
            CurrentRevision = 0,
        };

        var result = target.CurrentSolution;

        result.Should().BeSameAs(baselineSolution);
        target.CurrentSnapshotId.Should().Be(WorkspaceSnapshotTestFactory.CreateId(1));
    }

    [Fact]
    public void GIVEN_PositiveCurrentRevision_WHEN_GettingCurrentSolution_THEN_ShouldReturnSelectedRevisionSolution()
    {
        using var workspace = new AdhocWorkspace();
        var baselineSolution = workspace.CurrentSolution;
        var firstSolution = baselineSolution.AddProject("FirstProject", "FirstProject", LanguageNames.CSharp).Solution;
        var secondSolution = firstSolution.AddProject("SecondProject", "SecondProject", LanguageNames.CSharp).Solution;
        var target = new WorkspaceTransaction
        {
            TransactionId = new WorkspaceTransactionId(1),
            BaselineSnapshotId = WorkspaceSnapshotTestFactory.CreateId(1),
            BaselineSolution = baselineSolution,
            Revisions =
            [
                CreateRevision(firstSolution, 2),
                CreateRevision(secondSolution, 3),
            ],
            CurrentRevision = 2,
        };

        var result = target.CurrentSolution;

        result.Should().BeSameAs(secondSolution);
        target.CurrentSnapshotId.Should().Be(WorkspaceSnapshotTestFactory.CreateId(3));
    }

    [Fact]
    public void GIVEN_ZeroRevision_WHEN_ProjectingInfo_THEN_ShouldExposeInitialCapabilities()
    {
        var target = new WorkspaceTransaction
        {
            TransactionId = new WorkspaceTransactionId(1),
            BaselineSnapshotId = WorkspaceSnapshotTestFactory.CreateId(1),
            BaselineSolution = _workspace.CurrentSolution,
            CurrentRevision = 0,
            MaxRevisions = 3,
        };

        var result = target.ToInfo(conflicted: false);

        result.Revision.Should().Be(0);
        result.RevisionCount.Should().Be(0);
        result.MaxRevisions.Should().Be(3);
        result.RemainingRevisions.Should().Be(3);
        result.CanMutate.Should().BeTrue();
        result.CanUndo.Should().BeFalse();
        result.CanRedo.Should().BeFalse();
        result.CanCommit.Should().BeFalse();
        result.CanRollback.Should().BeTrue();
    }

    [Fact]
    public void GIVEN_MiddleRevisionWithLaterRevision_WHEN_ProjectingInfo_THEN_ShouldExposeUndoRedoAndCommitCapabilities()
    {
        var target = new WorkspaceTransaction
        {
            TransactionId = new WorkspaceTransactionId(1),
            BaselineSnapshotId = WorkspaceSnapshotTestFactory.CreateId(1),
            BaselineSolution = _workspace.CurrentSolution,
            Revisions = [CreateRevision(_workspace.CurrentSolution), CreateRevision(_workspace.CurrentSolution)],
            CurrentRevision = 1,
            MaxRevisions = 3,
        };

        var result = target.ToInfo(conflicted: false);

        result.Revision.Should().Be(1);
        result.RevisionCount.Should().Be(2);
        result.RemainingRevisions.Should().Be(2);
        result.CanMutate.Should().BeTrue();
        result.CanUndo.Should().BeTrue();
        result.CanRedo.Should().BeTrue();
        result.CanCommit.Should().BeTrue();
        result.CanRollback.Should().BeTrue();
    }

    [Fact]
    public void GIVEN_MaximumRevision_WHEN_ProjectingInfo_THEN_ShouldDisableMutationAndRedo()
    {
        var target = new WorkspaceTransaction
        {
            TransactionId = new WorkspaceTransactionId(1),
            BaselineSnapshotId = WorkspaceSnapshotTestFactory.CreateId(1),
            BaselineSolution = _workspace.CurrentSolution,
            Revisions = [CreateRevision(_workspace.CurrentSolution), CreateRevision(_workspace.CurrentSolution)],
            CurrentRevision = 2,
            MaxRevisions = 2,
        };

        var result = target.ToInfo(conflicted: false);

        result.RemainingRevisions.Should().Be(0);
        result.CanMutate.Should().BeFalse();
        result.CanUndo.Should().BeTrue();
        result.CanRedo.Should().BeFalse();
        result.CanCommit.Should().BeTrue();
    }

    [Fact]
    public void GIVEN_RevisionBeyondCapacity_WHEN_ProjectingInfo_THEN_ShouldClampRemainingRevisionsToZero()
    {
        var target = new WorkspaceTransaction
        {
            TransactionId = new WorkspaceTransactionId(1),
            BaselineSnapshotId = WorkspaceSnapshotTestFactory.CreateId(1),
            BaselineSolution = _workspace.CurrentSolution,
            Revisions =
            [
                CreateRevision(_workspace.CurrentSolution),
                CreateRevision(_workspace.CurrentSolution),
            ],
            CurrentRevision = 3,
            MaxRevisions = 2,
        };

        var result = target.ToInfo(conflicted: false);

        result.RemainingRevisions.Should().Be(0);
        result.CanMutate.Should().BeFalse();
        result.CanUndo.Should().BeTrue();
        result.CanRedo.Should().BeFalse();
        result.CanCommit.Should().BeTrue();
    }

    [Fact]
    public void GIVEN_ConflictedTransaction_WHEN_ProjectingInfo_THEN_ShouldDisableMutationAndCommit()
    {
        var target = new WorkspaceTransaction
        {
            TransactionId = new WorkspaceTransactionId(1),
            BaselineSnapshotId = WorkspaceSnapshotTestFactory.CreateId(1),
            BaselineSolution = _workspace.CurrentSolution,
            Revisions = [CreateRevision(_workspace.CurrentSolution)],
            CurrentRevision = 1,
            MaxRevisions = 3,
        };

        var result = target.ToInfo(conflicted: true);

        result.CanMutate.Should().BeFalse();
        result.CanUndo.Should().BeTrue();
        result.CanCommit.Should().BeFalse();
        result.CanRollback.Should().BeTrue();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    private static WorkspaceTransactionRevision CreateRevision(
        Solution solution,
        long snapshotId = 1)
    {
        return new WorkspaceTransactionRevision
        {
            SnapshotId = WorkspaceSnapshotTestFactory.CreateId(snapshotId),
            Solution = solution,
            Changes = new ChangeSummary(),
            Operation = "Operation",
            Summary = "Summary",
            Preview = new MutationPreview { Summary = "Summary" },
        };
    }
}
