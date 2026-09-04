using Roslyn.Workbench.Mcp.Workspace.Caching;
using Roslyn.Workbench.Mcp.Workspace.State;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Caching;

public sealed class PluginQueryCacheLifecycleObserverTests
{
    private readonly Mock<IPluginQueryCacheState> _state;
    private readonly PluginQueryCacheLifecycleObserver _target;

    public PluginQueryCacheLifecycleObserverTests()
    {
        _state = new Mock<IPluginQueryCacheState>();
        _target = new PluginQueryCacheLifecycleObserver(_state.Object);
    }

    [Fact]
    public void GIVEN_WorkspaceIdentity_WHEN_InvalidatingWorkspace_THEN_ShouldDelegateToState()
    {
        var workspaceId = Guid.NewGuid();

        _target.InvalidateWorkspace(workspaceId, 1);

        _state.Verify(item => item.InvalidateWorkspace(workspaceId, 1), Times.Once);
    }

    [Fact]
    public void GIVEN_TransactionIdentity_WHEN_InvalidatingTransaction_THEN_ShouldDelegateToState()
    {
        var workspaceId = Guid.NewGuid();
        var transactionId = new WorkspaceTransactionId(1);

        _target.InvalidateTransaction(workspaceId, 1, transactionId);

        _state.Verify(item => item.InvalidateTransaction(workspaceId, 1, transactionId), Times.Once);
    }

    [Fact]
    public void GIVEN_SnapshotIdentities_WHEN_InvalidatingSnapshots_THEN_ShouldDelegateToState()
    {
        IReadOnlyList<WorkspaceSnapshotIdentity> snapshots = [CreateSnapshotIdentity()];

        _target.InvalidateSnapshots(snapshots);

        _state.Verify(item => item.InvalidateSnapshots(snapshots), Times.Once);
    }

    private static WorkspaceSnapshotIdentity CreateSnapshotIdentity()
    {
        return new WorkspaceSnapshotIdentity(
            Guid.NewGuid(),
            1,
            new WorkspaceSnapshotId(Guid.NewGuid()),
            transactionId: null);
    }
}
