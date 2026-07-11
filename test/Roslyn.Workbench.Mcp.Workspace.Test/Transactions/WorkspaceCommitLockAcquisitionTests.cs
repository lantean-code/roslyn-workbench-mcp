namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class WorkspaceCommitLockAcquisitionTests
{
    [Fact]
    public void GIVEN_Ownership_WHEN_CreatingAcquiredResult_THEN_ShouldExposeAcquiredInvariant()
    {
        var ownership = new Mock<IWorkspaceCommitLock>();

        var result = WorkspaceCommitLockAcquisition.Acquired(ownership.Object);

        result.Status.Should().Be(WorkspaceCommitLockAcquisitionStatus.Acquired);
        result.IsAcquired.Should().BeTrue();
        result.IsContended.Should().BeFalse();
        result.IsFailed.Should().BeFalse();
        result.Lock.Should().BeSameAs(ownership.Object);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void GIVEN_Contention_WHEN_CreatingContendedResult_THEN_ShouldExposeContendedInvariant()
    {
        var result = WorkspaceCommitLockAcquisition.Contended();

        result.Status.Should().Be(WorkspaceCommitLockAcquisitionStatus.Contended);
        result.IsAcquired.Should().BeFalse();
        result.IsContended.Should().BeTrue();
        result.IsFailed.Should().BeFalse();
        result.Lock.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void GIVEN_Error_WHEN_CreatingFailedResult_THEN_ShouldExposeFailureInvariant()
    {
        var result = WorkspaceCommitLockAcquisition.Failed("failure");

        result.Status.Should().Be(WorkspaceCommitLockAcquisitionStatus.Failed);
        result.IsAcquired.Should().BeFalse();
        result.IsContended.Should().BeFalse();
        result.IsFailed.Should().BeTrue();
        result.Lock.Should().BeNull();
        result.ErrorMessage.Should().Be("failure");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void GIVEN_InvalidError_WHEN_CreatingFailedResult_THEN_ShouldThrowArgumentException(string errorMessage)
    {
        var action = () => WorkspaceCommitLockAcquisition.Failed(errorMessage);

        action.Should().Throw<ArgumentException>();
    }
}
