namespace Roslyn.Workbench.Mcp.Test.Transactions;

public sealed class CommitConfirmationStateTests
{
    [Fact]
    public void GIVEN_NewState_WHEN_ApprovingForSession_THEN_ShouldRetainApproval()
    {
        var target = new CommitConfirmationState();

        target.IsApprovedForSession.Should().BeFalse();

        target.ApproveForSession();

        target.IsApprovedForSession.Should().BeTrue();
    }
}
