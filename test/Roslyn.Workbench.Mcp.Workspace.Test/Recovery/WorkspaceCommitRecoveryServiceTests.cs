using Roslyn.Workbench.Mcp.Workspace.Recovery;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Recovery;

public sealed class WorkspaceCommitRecoveryServiceTests
{
    private readonly Mock<ICommitRecoveryStore> _store = new();
    private readonly Mock<IWorkspaceCommitWriter> _writer = new();
    private readonly Mock<IWorkspaceCommitLockManager> _lockManager = new();
    private readonly WorkspaceCommitRecoveryService _target;

    public WorkspaceCommitRecoveryServiceTests()
    {
        _store.Setup(item => item.GetOrphanedCommitOwnersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _lockManager.Setup(item => item.Acquire(It.IsAny<string>())).Returns(() => CreateAcquisition(lockAvailable: true));
        _writer.Setup(item => item.CompleteAsync(It.IsAny<WorkspaceCommitManifest>())).ReturnsAsync(true);
        _target = new WorkspaceCommitRecoveryService(_store.Object, _writer.Object, _lockManager.Object);
    }

    [Theory]
    [InlineData(RecoveryState.Committed)]
    [InlineData(RecoveryState.Restored)]
    public async Task GIVEN_TerminalManifest_WHEN_Recovering_THEN_ShouldCleanArtifacts(RecoveryState state)
    {
        var manifest = CreateManifest(state);
        _store.Setup(item => item.GetManifestsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([manifest]);

        await _target.RecoverAsync(TestContext.Current.CancellationToken);

        _store.Verify(item => item.DeleteStatus("commit"), Times.Once);
        _writer.Verify(item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>()), Times.Never);
        _writer.Verify(item => item.CompleteAsync(manifest), state == RecoveryState.Committed ? Times.Once() : Times.Never());
    }

    [Fact]
    public async Task GIVEN_ApplyingManifest_WHEN_RestorationSucceeds_THEN_ShouldPersistRestoredBeforeCleanup()
    {
        var manifest = CreateManifest(RecoveryState.Applying);
        _store.Setup(item => item.GetManifestsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([manifest]);
        _writer.Setup(item => item.RestoreAsync(manifest)).ReturnsAsync(RecoveryState.Restored);
        var sequence = new MockSequence();
        _store.InSequence(sequence).Setup(item => item.WriteManifestAsync(
            It.Is<WorkspaceCommitManifest>(value => value.State == RecoveryState.Restored),
            CancellationToken.None));

        _store.InSequence(sequence).Setup(item => item.DeleteStatus("commit"));

        await _target.RecoverAsync(TestContext.Current.CancellationToken);

        _store.VerifyAll();
    }

    [Theory]
    [InlineData(RecoveryState.RecoveryConflict)]
    [InlineData(RecoveryState.RecoveryIncomplete)]
    public async Task GIVEN_UnresolvedRestoration_WHEN_Recovering_THEN_ShouldRetainManifest(RecoveryState result)
    {
        var manifest = CreateManifest(RecoveryState.Applying);
        _store.Setup(item => item.GetManifestsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([manifest]);
        _writer.Setup(item => item.RestoreAsync(manifest)).ReturnsAsync(result);

        await _target.RecoverAsync(TestContext.Current.CancellationToken);

        _store.Verify(item => item.WriteManifestAsync(It.Is<WorkspaceCommitManifest>(value => value.State == result), CancellationToken.None), Times.Once);
        _store.Verify(item => item.DeleteStatus(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_RecoveryConflict_WHEN_RecoveringAgain_THEN_ShouldLeaveItForManualResolution()
    {
        var manifest = CreateManifest(RecoveryState.RecoveryConflict);
        _store.Setup(item => item.GetManifestsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([manifest]);

        await _target.RecoverAsync(TestContext.Current.CancellationToken);

        _writer.Verify(item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>()), Times.Never);
        _store.Verify(item => item.DeleteStatus(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GIVEN_PreManifestOwner_WHEN_Recovering_THEN_ShouldCleanOnlyAfterAcquiringSolutionLock(bool lockAvailable)
    {
        var owner = new WorkspaceCommitOwner
        {
            CommitId = "orphan",
            LoadedPath = "/workspace/orphan.slnx",
            WorkspaceRoot = "/workspace",
        };

        _store.Setup(item => item.GetOrphanedCommitOwnersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([owner]);
        _store.Setup(item => item.GetManifestsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _lockManager.Setup(item => item.Acquire(owner.WorkspaceRoot)).Returns(CreateAcquisition(lockAvailable));

        await _target.RecoverAsync(TestContext.Current.CancellationToken);

        _store.Verify(item => item.DeleteStatus("orphan"), lockAvailable ? Times.Once() : Times.Never());
    }

    [Fact]
    public async Task GIVEN_CommittedCleanupStillFails_WHEN_Recovering_THEN_ShouldRetainTerminalEvidenceForRetry()
    {
        var manifest = CreateManifest(RecoveryState.Committed);
        _store.Setup(item => item.GetManifestsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([manifest]);
        _writer.Setup(item => item.CompleteAsync(manifest)).ReturnsAsync(false);

        await _target.RecoverAsync(TestContext.Current.CancellationToken);

        _store.Verify(item => item.DeleteStatus(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_AnotherProcessOwnsCommitLock_WHEN_Recovering_THEN_ShouldLeaveManifestUntouched()
    {
        var manifest = CreateManifest(RecoveryState.Applying);
        _store.Setup(item => item.GetManifestsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([manifest]);
        _lockManager.Setup(item => item.Acquire(manifest.WorkspaceRoot)).Returns(CreateAcquisition(lockAvailable: false));

        await _target.RecoverAsync(TestContext.Current.CancellationToken);

        _writer.Verify(item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>()), Times.Never);
        _store.Verify(item => item.WriteManifestAsync(It.IsAny<WorkspaceCommitManifest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static WorkspaceCommitManifest CreateManifest(RecoveryState state)
    {
        return new()
        {
            CommitId = "commit",
            LoadedPath = "/workspace/solution.slnx",
            WorkspaceRoot = "/workspace",
            State = state,
            Entries = [],
            CreatedDirectories = [],
        };
    }

    private static WorkspaceCommitLockAcquisition CreateAcquisition(bool lockAvailable)
    {
        return lockAvailable
            ? WorkspaceCommitLockAcquisition.Acquired(new Mock<IWorkspaceCommitLock>().Object)
            : WorkspaceCommitLockAcquisition.Contended();
    }
}
