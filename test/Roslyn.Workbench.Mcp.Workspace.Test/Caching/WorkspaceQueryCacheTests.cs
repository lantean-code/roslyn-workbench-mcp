using Roslyn.Workbench.Mcp.Workspace.Caching;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Caching;

public sealed class WorkspaceQueryCacheTests
{
    private readonly Mock<IWorkspaceQueryCacheState> _workspaceState;
    private readonly WorkspaceQueryCache _target;

    public WorkspaceQueryCacheTests()
    {
        _workspaceState = new Mock<IWorkspaceQueryCacheState>();
        _target = new WorkspaceQueryCache(_workspaceState.Object);
    }

    [Fact]
    public void GIVEN_WorkspaceId_WHEN_InvalidatingWorkspace_THEN_ShouldDelegateToState()
    {
        _target.InvalidateWorkspace(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        _workspaceState.Verify(
            item => item.InvalidateWorkspace(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            Times.Once);
    }
}
