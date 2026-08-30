namespace Roslyn.Workbench.Mcp.Test.ErrorReporting.Capture;

public sealed class CapturedWorkspaceContextTests
{
    [Fact]
    public void GIVEN_ExplicitWorkspaceState_WHEN_CreatingCapturedContext_THEN_ShouldCopyImmutableWorkspaceState()
    {
        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            WorkspaceEpoch = 2,
            LoadedPath = "LoadedPath",
            WorkspaceRoot = "WorkspaceRoot",
        };

        var result = new CapturedWorkspaceContext(
            workspaceIdentity,
            WorkspaceLifecycleState.TransactionConflicted,
            projectCount: 5,
            documentCount: 6,
            transactionRevision: 4);

        result.WorkspaceId.Should().Be(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        result.WorkspaceEpoch.Should().Be(2);
        result.LifecycleState.Should().Be("TransactionConflicted");
        result.ProjectCount.Should().Be(5);
        result.DocumentCount.Should().Be(6);
        result.TransactionRevision.Should().Be(4);
    }
}
