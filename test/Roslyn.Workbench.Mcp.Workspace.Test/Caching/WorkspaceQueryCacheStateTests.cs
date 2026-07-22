using Roslyn.Workbench.Mcp.Workspace.Caching;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Caching;

public sealed class WorkspaceQueryCacheStateTests
{
    [Fact]
    public void GIVEN_WorkspaceToken_WHEN_InvalidatingWorkspace_THEN_ShouldCancelOnlyMatchingToken()
    {
        using var target = new WorkspaceQueryCacheState();
        var token = target.GetInvalidationToken("WorkspaceId");
        var sameWorkspaceToken = target.GetInvalidationToken("WorkspaceId");
        var otherWorkspaceToken = target.GetInvalidationToken("OtherWorkspaceId");

        target.InvalidateWorkspace("WorkspaceId");

        token.HasChanged.Should().BeTrue();
        sameWorkspaceToken.HasChanged.Should().BeTrue();
        otherWorkspaceToken.HasChanged.Should().BeFalse();
    }

    [Fact]
    public void GIVEN_InvalidatedWorkspace_WHEN_GettingTokenAgain_THEN_ShouldReturnActiveToken()
    {
        using var target = new WorkspaceQueryCacheState();
        var invalidatedToken = target.GetInvalidationToken("WorkspaceId");
        target.InvalidateWorkspace("WorkspaceId");

        var replacementToken = target.GetInvalidationToken("WorkspaceId");

        invalidatedToken.HasChanged.Should().BeTrue();
        replacementToken.HasChanged.Should().BeFalse();
    }

    [Fact]
    public void GIVEN_UnknownWorkspace_WHEN_InvalidatingWorkspace_THEN_ShouldRemainUsable()
    {
        using var target = new WorkspaceQueryCacheState();

        target.InvalidateWorkspace("WorkspaceId");

        target.GetInvalidationToken("WorkspaceId").HasChanged.Should().BeFalse();
    }

    [Fact]
    public void GIVEN_WorkspaceTokens_WHEN_Disposing_THEN_ShouldCancelAllTokens()
    {
        var target = new WorkspaceQueryCacheState();
        var firstToken = target.GetInvalidationToken("FirstWorkspaceId");
        var secondToken = target.GetInvalidationToken("SecondWorkspaceId");

        target.Dispose();

        firstToken.HasChanged.Should().BeTrue();
        secondToken.HasChanged.Should().BeTrue();
    }
}
