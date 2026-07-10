namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class WorkspaceTransactionTests
{
    [Fact]
    public void GIVEN_ZeroCurrentRevision_WHEN_GettingCurrentSolution_THEN_ShouldReturnBaselineSolution()
    {
        using var workspace = new AdhocWorkspace();
        var baselineSolution = workspace.CurrentSolution;
        var target = new WorkspaceTransaction
        {
            BaselineSolution = baselineSolution,
            CurrentRevision = 0,
        };

        var result = target.CurrentSolution;

        result.Should().BeSameAs(baselineSolution);
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
            BaselineSolution = baselineSolution,
            Revisions =
            [
                CreateRevision(firstSolution),
                CreateRevision(secondSolution),
            ],
            CurrentRevision = 2,
        };

        var result = target.CurrentSolution;

        result.Should().BeSameAs(secondSolution);
    }

    [Fact]
    public void GIVEN_ZeroRevision_WHEN_ProjectingInfo_THEN_ShouldExposeInitialCapabilities()
    {
        var target = new WorkspaceTransaction
        {
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
            Revisions = [new WorkspaceTransactionRevision(), new WorkspaceTransactionRevision()],
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
            Revisions = [new WorkspaceTransactionRevision(), new WorkspaceTransactionRevision()],
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
            Revisions =
            [
                new WorkspaceTransactionRevision(),
                new WorkspaceTransactionRevision(),
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
            Revisions = [new WorkspaceTransactionRevision()],
            CurrentRevision = 1,
            MaxRevisions = 3,
        };

        var result = target.ToInfo(conflicted: true);

        result.CanMutate.Should().BeFalse();
        result.CanUndo.Should().BeTrue();
        result.CanCommit.Should().BeFalse();
        result.CanRollback.Should().BeTrue();
    }

    private static WorkspaceTransactionRevision CreateRevision(Solution solution)
    {
        return new WorkspaceTransactionRevision
        {
            Solution = solution,
        };
    }
}
