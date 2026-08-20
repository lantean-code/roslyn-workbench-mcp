using Roslyn.Workbench.Mcp.Workspace.ChangeDetection;
using Roslyn.Workbench.Mcp.Workspace.Coordination;
using Roslyn.Workbench.Mcp.Workspace.Loading;
using Roslyn.Workbench.Mcp.Workspace.Recovery;
using Roslyn.Workbench.Mcp.Workspace.Selection;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class TransactionCommitServiceTests : IDisposable
{
    private readonly AdhocWorkspace _workspace = new();
    private readonly Mock<IWorkspaceSessionStore> _sessionStore = new();
    private readonly Mock<IWorkspaceChangeDetector> _changeDetector = new();
    private readonly Mock<IWorkspaceInputCertification> _applicationCertification = new();
    private readonly Mock<IWorkspaceInputCertification> _promotionCertification = new();
    private readonly WorkspaceInputManifest _applicationInputManifest = new();
    private readonly Mock<IWorkspaceStateTransitions> _stateTransitions = new();
    private readonly Mock<ISnapshotGuard> _snapshotGuard = new();
    private readonly Mock<IWorkspaceOperationResultFactory> _resultFactory = new();
    private readonly Mock<ICommitRecoveryStore> _recoveryStore = new();
    private readonly Mock<IWorkspaceCommitWriter> _commitWriter = new();
    private readonly Mock<IWorkspaceCommitPlanner> _planner = new();
    private readonly Mock<IWorkspaceCommitLockManager> _lockManager = new();
    private readonly Mock<IWorkspaceInstanceStatusPublisher> _statusPublisher = new();
    private readonly TransactionCommitService _target;

    public TransactionCommitServiceTests()
    {
        _changeDetector
            .SetupSequence(item => item.BeginCertification(It.IsAny<string>()))
            .Returns(_applicationCertification.Object)
            .Returns(_promotionCertification.Object);
        _applicationCertification
            .Setup(item => item.Complete(
                It.IsAny<WorkspaceInputManifest>(),
                It.IsAny<IEnumerable<string>>()))
            .Returns(_applicationInputManifest);
        _commitWriter
            .Setup(item => item.ValidateAppliedStateAsync(It.IsAny<WorkspaceCommitManifest>()))
            .ReturnsAsync(WorkspaceCommitValidationResult.Valid());
        _sessionStore
            .Setup(item => item.AllocateWorkspaceSnapshotId())
            .Returns(new WorkspaceSnapshotId(3));
        _sessionStore
            .Setup(item => item.TryCompleteTransaction(It.IsAny<WorkspaceSessionSnapshot>()))
            .Returns(TransactionCompletionResult.Completed());

        _recoveryStore
            .Setup(item => item.PersistPlanAsync(
                It.IsAny<WorkspaceCommitPlan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CommitRecoveryPlanPersistenceResult.Persisted());

        _target = new TransactionCommitService(
            _sessionStore.Object,
            _changeDetector.Object,
            _stateTransitions.Object,
            _snapshotGuard.Object,
            _resultFactory.Object,
            _recoveryStore.Object,
            _commitWriter.Object,
            _planner.Object,
            _lockManager.Object,
            _statusPublisher.Object);
    }

    [Fact]
    public async Task GIVEN_RecoveryPayloadCapacityExceeded_WHEN_Committing_THEN_ShouldRejectWithoutRecovery()
    {
        var session = CreateSession();
        var manifest = CreateManifest();
        var plan = new WorkspaceCommitPlan(manifest, new Dictionary<string, ReadOnlyMemory<byte>>());
        var expected = CreateResult(WorkspaceOperationStatus.Rejected);
        var transaction = session.Transaction
            ?? throw new InvalidOperationException("The test session must have an active transaction.");

        SetupProtocol(session, plan);
        _recoveryStore
            .Setup(item => item.PersistPlanAsync(plan, TestContext.Current.CancellationToken))
            .ReturnsAsync(CommitRecoveryPlanPersistenceResult.CapacityExceeded(
                "The recovery artifact 'staged/File.bin' requires 3 bytes, exceeding the supported maximum of 2 bytes."));

        _resultFactory.Setup(item => item.Rejected<TransactionCommitOutcome>(
            WorkspaceErrorCodes.CommitRecoveryCapacity,
            "The transaction cannot be committed because its recovery data exceeds a supported size limit. Roll back this transaction and stage a smaller change. The recovery artifact 'staged/File.bin' requires 3 bytes, exceeding the supported maximum of 2 bytes.",
            RequiredAction.RollbackTransaction,
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var result = await _target.CommitAsync(
            CreateSelection(session),
            expectedSnapshot: null,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _statusPublisher.Verify(item => item.UpdateAsync(
            session.Workspace.WorkspaceId,
            session.State,
            transaction.CurrentRevision,
            It.IsAny<string>(),
            "Staging"), Times.Once);

        _statusPublisher.Verify(item => item.UpdateAsync(
            session.Workspace.WorkspaceId,
            session.State,
            transaction.CurrentRevision,
            null,
            null), Times.Once);

        _commitWriter.Verify(item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>()), Times.Never);
        _commitWriter.Verify(item => item.RevalidateAsync(It.IsAny<WorkspaceCommitManifest>(), It.IsAny<CancellationToken>()), Times.Never);
        _commitWriter.Verify(item => item.ApplyAsync(It.IsAny<WorkspaceCommitManifest>()), Times.Never);
        _recoveryStore.Verify(item => item.WriteManifestAsync(
            It.IsAny<WorkspaceCommitManifest>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _sessionStore.Verify(item => item.ReplaceSession(It.IsAny<WorkspaceSessionSnapshot>()), Times.Never);
        _sessionStore.Verify(item => item.TryCompleteTransaction(It.IsAny<WorkspaceSessionSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_NoActiveTransaction_WHEN_Committing_THEN_ShouldRequireTransaction()
    {
        var session = CreateSession();
        session = session with
        {
            Transaction = null,
            CurrentSnapshotIdentity = WorkspaceSnapshotIdentity.Create(
                session.Workspace,
                session.CommittedSnapshotId,
                transaction: null),
        };

        var expected = CreateResult(WorkspaceOperationStatus.Rejected);
        _sessionStore.Setup(item => item.ReadSession(Guid.Parse("11111111-1111-1111-1111-111111111111"))).Returns(session);
        _resultFactory.Setup(item => item.Rejected<TransactionCommitOutcome>(
            WorkspaceErrorCodes.TransactionRequired, It.IsAny<string>(), RequiredAction.StartTransaction, null, null, null)).Returns(expected);

        var result = await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_MissingWorkspaceSession_WHEN_Committing_THEN_ShouldRequireTransaction()
    {
        var selection = CreateSelection(CreateSession());
        var expected = CreateResult(WorkspaceOperationStatus.Rejected);
        _sessionStore.Setup(item => item.ReadSession(Guid.Parse("11111111-1111-1111-1111-111111111111"))).Returns((WorkspaceSessionSnapshot?)null);
        _resultFactory.Setup(item => item.Rejected<TransactionCommitOutcome>(
            WorkspaceErrorCodes.TransactionRequired, It.IsAny<string>(), RequiredAction.StartTransaction, null, null, null)).Returns(expected);

        var result = await _target.CommitAsync(selection, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_SnapshotMismatch_WHEN_Committing_THEN_ShouldReturnConflictWithoutWriting()
    {
        var session = CreateSession();
        var mismatch = new WorkspaceOperationError { Code = "SnapshotMismatch", Message = "Mismatch" };
        var expected = CreateResult(WorkspaceOperationStatus.Conflict);
        _sessionStore.Setup(item => item.ReadSession(Guid.Parse("11111111-1111-1111-1111-111111111111"))).Returns(session);
        _snapshotGuard.Setup(item => item.Validate(session, It.IsAny<SnapshotPrecondition?>())).Returns(mismatch);
        _resultFactory.Setup(item => item.Conflict<TransactionCommitOutcome>(mismatch, It.IsAny<WorkspaceOperationContext>(), null, null)).Returns(expected);

        var expectedSnapshot = new SnapshotPrecondition { WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111") };
        var result = await _target.CommitAsync(CreateSelection(session), expectedSnapshot, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _lockManager.Verify(item => item.Acquire(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ConflictedTransaction_WHEN_Committing_THEN_ShouldRequireRollback()
    {
        var session = CreateSession() with { State = WorkspaceLifecycleState.TransactionConflicted };
        var expected = CreateResult(WorkspaceOperationStatus.Conflict);
        _sessionStore.Setup(item => item.ReadSession(Guid.Parse("11111111-1111-1111-1111-111111111111"))).Returns(session);
        _resultFactory.Setup(item => item.Conflict<TransactionCommitOutcome>(
            WorkspaceErrorCodes.TransactionConflicted, It.IsAny<string>(), RequiredAction.RollbackTransaction,
            It.IsAny<WorkspaceOperationContext>(), null, null)).Returns(expected);

        var result = await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_EmptyTransaction_WHEN_Committing_THEN_ShouldReturnNoChange()
    {
        var session = CreateSession();
        var empty = session.Transaction! with { CurrentRevision = 0 };
        session = session with
        {
            Transaction = empty,
            CurrentSolution = empty.BaselineSolution,
            CurrentSnapshotIdentity = WorkspaceSnapshotIdentity.Create(
                session.Workspace,
                session.CommittedSnapshotId,
                empty),
        };

        var expected = CreateResult(WorkspaceOperationStatus.NoChange);
        _sessionStore.Setup(item => item.ReadSession(Guid.Parse("11111111-1111-1111-1111-111111111111"))).Returns(session);
        _resultFactory.Setup(item => item.NoChange(
            It.IsAny<WorkspaceOperationContext>(), It.Is<TransactionCommitOutcome>(outcome => !outcome.Committed), null, null)).Returns(expected);

        var result = await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_ExternalWorkspaceChange_WHEN_Committing_THEN_ShouldConflictAndRetainTransaction()
    {
        var session = CreateSession();
        var conflicted = session with { State = WorkspaceLifecycleState.TransactionConflicted };
        var expected = CreateResult(WorkspaceOperationStatus.Conflict);
        _sessionStore.Setup(item => item.ReadSession(Guid.Parse("11111111-1111-1111-1111-111111111111"))).Returns(session);
        _changeDetector.Setup(item => item.HasChanged(session.InputManifest, It.IsAny<CancellationToken>())).Returns(true);
        _stateTransitions.Setup(item => item.ApplyExternalChangeDetected(session)).Returns(conflicted);
        _resultFactory.Setup(item => item.Conflict<TransactionCommitOutcome>(
            WorkspaceErrorCodes.TransactionConflicted, It.IsAny<string>(), RequiredAction.RollbackTransaction,
            It.IsAny<WorkspaceOperationContext>(), null, null)).Returns(expected);

        var result = await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _sessionStore.Verify(item => item.ReplaceSession(conflicted), Times.Once);
        _statusPublisher.Verify(item => item.QueueUpdate(
            conflicted.Workspace.WorkspaceId,
            WorkspaceLifecycleState.TransactionConflicted,
            conflicted.Transaction!.CurrentRevision,
            null,
            null), Times.Once);
    }

    [Fact]
    public async Task GIVEN_CommitLockContention_WHEN_Committing_THEN_ShouldReturnRetryableWorkspaceBusy()
    {
        var session = CreateSession();
        var expected = CreateResult(WorkspaceOperationStatus.Rejected);
        _sessionStore.Setup(item => item.ReadSession(Guid.Parse("11111111-1111-1111-1111-111111111111"))).Returns(session);
        _lockManager.Setup(item => item.Acquire("/workspace")).Returns(CreateLockAcquisition(lockAvailable: false));
        _resultFactory.Setup(item => item.Rejected<TransactionCommitOutcome>(
            WorkspaceErrorCodes.WorkspaceBusy,
            It.IsAny<string>(),
            RequiredAction.Retry,
            null,
            null,
            null)).Returns(expected);

        var result = await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _planner.Verify(item => item.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Solution>(), It.IsAny<Solution>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_CommitLockFailure_WHEN_Committing_THEN_ShouldReturnRetryableFault()
    {
        var session = CreateSession();
        var expected = CreateResult(WorkspaceOperationStatus.Faulted);
        _sessionStore.Setup(item => item.ReadSession(Guid.Parse("11111111-1111-1111-1111-111111111111"))).Returns(session);
        _lockManager.Setup(item => item.Acquire("/workspace")).Returns(WorkspaceCommitLockAcquisition.Failed("failure"));
        _resultFactory.Setup(item => item.Faulted<TransactionCommitOutcome>(
            "CommitLockFailed",
            "failure",
            RequiredAction.Retry,
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var result = await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _planner.Verify(item => item.CreateAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Solution>(),
            It.IsAny<Solution>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ValidTransaction_WHEN_Committing_THEN_ShouldPersistProtocolAndPromoteStagedSolution()
    {
        var properties = new WorkspaceMsBuildProperties
        {
            ArtifactsPath = "/artifacts",
        };

        var session = CreateSession() with
        {
            MsBuildProperties = properties,
        };

        var transaction = session.Transaction!;
        var commitLock = new Mock<IWorkspaceCommitLock>();
        var manifest = CreateManifest();
        var plan = new WorkspaceCommitPlan(manifest, new Dictionary<string, ReadOnlyMemory<byte>>());
        var expected = CreateResult(WorkspaceOperationStatus.Succeeded);
        using var inputManifest = new WorkspaceInputManifest();
        _sessionStore.Setup(item => item.ReadSession(Guid.Parse("11111111-1111-1111-1111-111111111111"))).Returns(session);
        _lockManager.Setup(item => item.Acquire("/workspace")).Returns(WorkspaceCommitLockAcquisition.Acquired(commitLock.Object));
        _planner.Setup(item => item.CreateAsync(
            It.IsAny<string>(), "/workspace/solution.slnx", "/workspace", transaction.BaselineSolution, transaction.CurrentSolution, TestContext.Current.CancellationToken))
            .ReturnsAsync(WorkspaceCommitPlanResult.Succeeded(plan));

        _commitWriter.Setup(item => item.RevalidateAsync(manifest, TestContext.Current.CancellationToken))
            .ReturnsAsync(WorkspaceCommitValidationResult.Valid());

        _commitWriter.Setup(item => item.ApplyAsync(It.IsAny<WorkspaceCommitManifest>()))
            .ReturnsAsync(WorkspaceCommitValidationResult.Valid());

        _changeDetector
            .Setup(item => item.BuildManifest(
                transaction.CurrentSolution,
                "/workspace/solution.slnx",
                "/workspace",
                _promotionCertification.Object,
                properties))
            .Returns(inputManifest);

        _stateTransitions.Setup(item => item.Fire(WorkspaceLifecycleState.TransactionActive, WorkspaceTrigger.TransactionCommitted)).Returns(WorkspaceLifecycleState.Ready);
        _commitWriter.Setup(item => item.CompleteAsync(It.IsAny<WorkspaceCommitManifest>())).ReturnsAsync(true);
        _resultFactory.Setup(item => item.Succeeded(
            It.Is<TransactionCommitOutcome>(outcome => outcome.Committed),
            It.IsAny<WorkspaceOperationContext>(), null, null)).Returns(expected);

        var result = await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _recoveryStore.Verify(item => item.PersistPlanAsync(plan, TestContext.Current.CancellationToken), Times.Once);
        _commitWriter.Verify(item => item.RevalidateAsync(manifest, TestContext.Current.CancellationToken), Times.Once);
        _recoveryStore.Verify(item => item.WriteManifestAsync(It.Is<WorkspaceCommitManifest>(value => value.State == RecoveryState.Applying), TestContext.Current.CancellationToken), Times.Once);
        _commitWriter.Verify(item => item.ApplyAsync(It.Is<WorkspaceCommitManifest>(value => value.State == RecoveryState.Applying)), Times.Once);
        _recoveryStore.Verify(item => item.WriteManifestAsync(It.Is<WorkspaceCommitManifest>(value => value.State == RecoveryState.Committed), CancellationToken.None), Times.Once);
        _commitWriter.Verify(item => item.CompleteAsync(It.Is<WorkspaceCommitManifest>(value => value.State == RecoveryState.Committed)), Times.Once);
        _sessionStore.Verify(item => item.TryCompleteTransaction(
            It.Is<WorkspaceSessionSnapshot>(value =>
                value.Transaction == null
                && value.CommittedSnapshotId == new WorkspaceSnapshotId(3)
                && value.CurrentSolution == transaction.CurrentSolution
                && value.InputManifest == inputManifest
                && value.CurrentSnapshotIdentity.TransactionId == null
                && value.CurrentSnapshotIdentity.SnapshotId == new WorkspaceSnapshotId(3))), Times.Once);

        _recoveryStore.Verify(item => item.DeleteStatus(It.IsAny<string>()), Times.Once);
        commitLock.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_InputEvaluationFailureAfterApplyingCommit_WHEN_Committing_THEN_ShouldMarkCommittedWorkspaceOutOfDate()
    {
        var session = CreateSession();
        var transaction = session.Transaction!;
        var manifest = CreateManifest();
        var plan = new WorkspaceCommitPlan(manifest, new Dictionary<string, ReadOnlyMemory<byte>>());
        using var inputManifest = new WorkspaceInputManifest
        {
            EvaluationFailures =
            [
                new WorkspaceProjectInputFailure
                {
                    ProjectPath = "/workspace/project.csproj",
                    Message = "Evaluation failed.",
                },
            ],
        };

        var expected = CreateResult(WorkspaceOperationStatus.Succeeded);
        SetupProtocol(session, plan);
        _changeDetector.Setup(item => item.BuildManifest(transaction.CurrentSolution, "/workspace/solution.slnx", "/workspace", _promotionCertification.Object))
            .Returns(inputManifest);

        _stateTransitions.Setup(item => item.ApplyExternalChangeDetected(It.Is<WorkspaceSessionSnapshot>(value =>
                value.State == WorkspaceLifecycleState.Ready
                && value.Transaction == null
                && value.CurrentSolution == transaction.CurrentSolution
                && value.InputManifest == inputManifest
                && value.LoadDiagnostics.Count == 1
                && value.LoadDiagnostics[0].Id == "WorkspaceInputEvaluationFailed")))
            .Returns((WorkspaceSessionSnapshot value) => value with { State = WorkspaceLifecycleState.WorkspaceOutOfDate });

        _resultFactory.Setup(item => item.Succeeded(
            It.Is<TransactionCommitOutcome>(outcome => outcome.Committed),
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var result = await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _sessionStore.Verify(item => item.TryCompleteTransaction(
            It.Is<WorkspaceSessionSnapshot>(value =>
                value.State == WorkspaceLifecycleState.WorkspaceOutOfDate
                && value.Transaction == null
                && value.CurrentSolution == transaction.CurrentSolution
                && value.InputManifest == inputManifest
                && value.LoadDiagnostics.Count == 1
                && value.LoadDiagnostics[0].Id == "WorkspaceInputEvaluationFailed")), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ApplyFailure_WHEN_Committing_THEN_ShouldRestoreNonCancellablyAndReturnRetryableFault()
    {
        var session = CreateSession();
        var manifest = CreateManifest();
        var plan = new WorkspaceCommitPlan(manifest, new Dictionary<string, ReadOnlyMemory<byte>>());
        var expected = CreateResult(WorkspaceOperationStatus.Faulted);
        _sessionStore.Setup(item => item.ReadSession(Guid.Parse("11111111-1111-1111-1111-111111111111"))).Returns(session);
        _lockManager.Setup(item => item.Acquire(It.IsAny<string>())).Returns(CreateLockAcquisition(lockAvailable: true));
        _planner.Setup(item => item.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Solution>(), It.IsAny<Solution>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkspaceCommitPlanResult.Succeeded(plan));

        _commitWriter.Setup(item => item.RevalidateAsync(manifest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkspaceCommitValidationResult.Valid());

        _commitWriter.Setup(item => item.ApplyAsync(It.IsAny<WorkspaceCommitManifest>()))
            .ThrowsAsync(new IOException("Apply failed.", new InvalidOperationException("File is in use.")));

        _commitWriter.Setup(item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>())).ReturnsAsync(RecoveryState.Restored);
        _resultFactory.Setup(item => item.Faulted<TransactionCommitOutcome>(
            "CommitFailed",
            "The transaction commit failed and its changes were restored or retained for recovery. Failure: Apply failed. File is in use.",
            RequiredAction.Retry,
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var result = await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _commitWriter.Verify(item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>()), Times.Once);
        _recoveryStore.Verify(item => item.WriteManifestAsync(It.Is<WorkspaceCommitManifest>(value => value.State == RecoveryState.Restored), CancellationToken.None), Times.Once);
        _recoveryStore.Verify(item => item.DeleteStatus(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_PlannerDetectsTargetDrift_WHEN_Committing_THEN_ShouldConflictWithoutPersistingRecovery()
    {
        var session = CreateSession();
        var transaction = session.Transaction ?? throw new InvalidOperationException("The transaction was not created.");
        var conflictedSession = session with { State = WorkspaceLifecycleState.TransactionConflicted };
        var expected = CreateResult(WorkspaceOperationStatus.Conflict);
        _sessionStore.Setup(item => item.ReadSession(Guid.Parse("11111111-1111-1111-1111-111111111111"))).Returns(session);
        _lockManager.Setup(item => item.Acquire("/workspace")).Returns(CreateLockAcquisition(lockAvailable: true));
        _planner.Setup(item => item.CreateAsync(
            It.IsAny<string>(),
            "/workspace/solution.slnx",
            "/workspace",
            transaction.BaselineSolution,
            transaction.CurrentSolution,
            TestContext.Current.CancellationToken))
            .ReturnsAsync(WorkspaceCommitPlanResult.Failed("Target changed."));

        _stateTransitions.Setup(item => item.ApplyExternalChangeDetected(session)).Returns(conflictedSession);
        _resultFactory.Setup(item => item.Conflict<TransactionCommitOutcome>(
            WorkspaceErrorCodes.TransactionConflicted,
            It.IsAny<string>(),
            RequiredAction.RollbackTransaction,
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var selection = CreateSelection(session);
        var result = await _target.CommitAsync(selection, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _sessionStore.Verify(item => item.ReplaceSession(conflictedSession), Times.Once);
        _recoveryStore.Verify(
            item => item.PersistPlanAsync(It.IsAny<WorkspaceCommitPlan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_PreApplyRevalidationDetectsTargetDrift_WHEN_Committing_THEN_ShouldDiscardPreparedJournalAndConflict()
    {
        var session = CreateSession();
        var conflictedSession = session with { State = WorkspaceLifecycleState.TransactionConflicted };
        var manifest = CreateManifest();
        var plan = new WorkspaceCommitPlan(manifest, new Dictionary<string, ReadOnlyMemory<byte>>());
        var expected = CreateResult(WorkspaceOperationStatus.Conflict);
        SetupProtocol(session, plan);
        _commitWriter.Setup(item => item.RevalidateAsync(manifest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkspaceCommitValidationResult.Invalid("Target changed."));

        _stateTransitions.Setup(item => item.ApplyExternalChangeDetected(session)).Returns(conflictedSession);
        _resultFactory.Setup(item => item.Conflict<TransactionCommitOutcome>(
            WorkspaceErrorCodes.TransactionConflicted,
            It.IsAny<string>(),
            RequiredAction.RollbackTransaction,
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var selection = CreateSelection(session);
        var result = await _target.CommitAsync(selection, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _recoveryStore.Verify(item => item.DeleteStatus(It.IsAny<string>()), Times.Once);
        _commitWriter.Verify(item => item.ApplyAsync(It.IsAny<WorkspaceCommitManifest>()), Times.Never);
        _commitWriter.Verify(item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ApplyRevalidationDetectsTargetDriftAndRestores_WHEN_Committing_THEN_ShouldConflict()
    {
        var session = CreateSession();
        var conflictedSession = session with { State = WorkspaceLifecycleState.TransactionConflicted };
        var manifest = CreateManifest();
        var plan = new WorkspaceCommitPlan(manifest, new Dictionary<string, ReadOnlyMemory<byte>>());
        var expected = CreateResult(WorkspaceOperationStatus.Conflict);
        SetupProtocol(session, plan);
        _commitWriter.Setup(item => item.ApplyAsync(It.IsAny<WorkspaceCommitManifest>()))
            .ReturnsAsync(WorkspaceCommitValidationResult.Invalid("Target changed."));

        _commitWriter.Setup(item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>()))
            .ReturnsAsync(RecoveryState.Restored);

        _stateTransitions.Setup(item => item.ApplyExternalChangeDetected(session)).Returns(conflictedSession);
        _resultFactory.Setup(item => item.Conflict<TransactionCommitOutcome>(
            WorkspaceErrorCodes.TransactionConflicted,
            It.IsAny<string>(),
            RequiredAction.RollbackTransaction,
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var selection = CreateSelection(session);
        var result = await _target.CommitAsync(selection, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _commitWriter.Verify(item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>()), Times.Once);
        _recoveryStore.Verify(item => item.WriteManifestAsync(
            It.Is<WorkspaceCommitManifest>(value =>
                value.State == RecoveryState.Restored
                && value.Message == "Target changed."),
            CancellationToken.None), Times.Once);

        _sessionStore.Verify(item => item.ReplaceSession(conflictedSession), Times.Once);
    }

    [Fact]
    public async Task GIVEN_AppliedStateDriftsBeforeCertification_WHEN_Committing_THEN_ShouldRestoreAndConflict()
    {
        var session = CreateSession();
        var conflictedSession = session with { State = WorkspaceLifecycleState.TransactionConflicted };
        var manifest = CreateManifest();
        var plan = new WorkspaceCommitPlan(manifest, new Dictionary<string, ReadOnlyMemory<byte>>());
        var expected = CreateResult(WorkspaceOperationStatus.Conflict);
        SetupProtocol(session, plan);
        _commitWriter.Setup(item => item.ValidateAppliedStateAsync(It.IsAny<WorkspaceCommitManifest>()))
            .ReturnsAsync(WorkspaceCommitValidationResult.Invalid("Target changed."));

        _commitWriter.Setup(item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>()))
            .ReturnsAsync(RecoveryState.Restored);

        _stateTransitions.Setup(item => item.ApplyExternalChangeDetected(session)).Returns(conflictedSession);
        _resultFactory.Setup(item => item.Conflict<TransactionCommitOutcome>(
            WorkspaceErrorCodes.TransactionConflicted,
            It.IsAny<string>(),
            RequiredAction.RollbackTransaction,
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var selection = CreateSelection(session);
        var result = await _target.CommitAsync(selection, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _changeDetector.Verify(
            item => item.BuildManifest(
                It.IsAny<Solution>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IWorkspaceInputCertification>()),
            Times.Never);

        _commitWriter.Verify(item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>()), Times.Once);
        _sessionStore.Verify(item => item.ReplaceSession(conflictedSession), Times.Once);
    }

    [Fact]
    public async Task GIVEN_WorkspaceInputsChangeDuringCommitPromotion_WHEN_Committing_THEN_ShouldRestoreAndConflict()
    {
        var session = CreateSession();
        var transaction = session.Transaction ?? throw new InvalidOperationException("The transaction was not created.");
        var conflictedSession = session with { State = WorkspaceLifecycleState.TransactionConflicted };
        var manifest = CreateManifest();
        var plan = new WorkspaceCommitPlan(manifest, new Dictionary<string, ReadOnlyMemory<byte>>());
        using var inputManifest = new WorkspaceInputManifest();
        inputManifest.RecordChange(new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.FileSystemWatcher,
            ErrorCode = WorkspaceInputChangeErrorCode.WatcherBufferOverflow,
            Kind = WorkspaceInputChangeKind.WatcherError,
        });

        var expected = CreateResult(WorkspaceOperationStatus.Conflict);
        SetupProtocol(session, plan);
        _changeDetector
            .Setup(item => item.BuildManifest(
                transaction.CurrentSolution,
                "/workspace/solution.slnx",
                "/workspace",
                _promotionCertification.Object))
            .Returns(inputManifest);

        _changeDetector.Setup(item => item.HasChanged(inputManifest, CancellationToken.None))
            .Returns(true);

        _commitWriter.Setup(item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>()))
            .ReturnsAsync(RecoveryState.Restored);

        _stateTransitions.Setup(item => item.ApplyExternalChangeDetected(session)).Returns(conflictedSession);
        _resultFactory.Setup(item => item.Conflict<TransactionCommitOutcome>(
            WorkspaceErrorCodes.TransactionConflicted,
            "Workspace inputs changed during commit promotion. Certification: Promotion. Detection source: FileSystemWatcher. Change kind: WatcherError. Error code: WatcherBufferOverflow.",
            RequiredAction.RollbackTransaction,
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var selection = CreateSelection(session);
        var result = await _target.CommitAsync(selection, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _commitWriter.Verify(item => item.ValidateAppliedStateAsync(It.IsAny<WorkspaceCommitManifest>()), Times.Once);
        _commitWriter.Verify(item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>()), Times.Once);
        _sessionStore.Verify(item => item.ReplaceSession(conflictedSession), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ChangedPromotionInputsWithoutRecordedDetails_WHEN_Committing_THEN_ShouldReportUnavailableDetails()
    {
        var session = CreateSession();
        var transaction = session.Transaction ?? throw new InvalidOperationException("The transaction was not created.");
        var conflictedSession = session with { State = WorkspaceLifecycleState.TransactionConflicted };
        var manifest = CreateManifest();
        var plan = new WorkspaceCommitPlan(manifest, new Dictionary<string, ReadOnlyMemory<byte>>());
        using var inputManifest = new WorkspaceInputManifest();
        var expected = CreateResult(WorkspaceOperationStatus.Conflict);
        SetupProtocol(session, plan);
        _changeDetector
            .Setup(item => item.BuildManifest(
                transaction.CurrentSolution,
                "/workspace/solution.slnx",
                "/workspace",
                _promotionCertification.Object))
            .Returns(inputManifest);

        _changeDetector.Setup(item => item.HasChanged(inputManifest, CancellationToken.None))
            .Returns(true);

        _commitWriter.Setup(item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>()))
            .ReturnsAsync(RecoveryState.Restored);

        _stateTransitions.Setup(item => item.ApplyExternalChangeDetected(session)).Returns(conflictedSession);
        _resultFactory.Setup(item => item.Conflict<TransactionCommitOutcome>(
            WorkspaceErrorCodes.TransactionConflicted,
            "Workspace inputs changed during commit promotion. Certification: Promotion. Detection details were unavailable.",
            RequiredAction.RollbackTransaction,
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var selection = CreateSelection(session);
        var result = await _target.CommitAsync(selection, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _commitWriter.Verify(item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>()), Times.Once);
        _sessionStore.Verify(item => item.ReplaceSession(conflictedSession), Times.Once);
    }

    [Fact]
    public async Task GIVEN_UnrelatedWorkspaceInputChangesDuringCommitApplication_WHEN_Committing_THEN_ShouldRestoreAndConflict()
    {
        var session = CreateSession();
        var conflictedSession = session with { State = WorkspaceLifecycleState.TransactionConflicted };
        var entry = new WorkspaceCommitEntry
        {
            TargetPath = "/workspace/Document.cs",
            Operation = WorkspaceFileOperation.Replace,
            OriginalExists = true,
            BackupPath = "/workspace/.recovery/backup",
            StagedPath = "/workspace/.recovery/staged",
        };

        var manifest = CreateManifest() with
        {
            Entries = [entry],
            CreatedDirectories = ["/workspace/Created"],
        };

        var plan = new WorkspaceCommitPlan(manifest, new Dictionary<string, ReadOnlyMemory<byte>>());
        var expected = CreateResult(WorkspaceOperationStatus.Conflict);
        SetupProtocol(session, plan);
        _applicationInputManifest.RecordChange(new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.FileSystemWatcher,
            Kind = WorkspaceInputChangeKind.Renamed,
            Path = "/workspace/Renamed.cs",
            PreviousPath = "/workspace/Document.cs",
        });

        _changeDetector.Setup(item => item.HasChanged(_applicationInputManifest, CancellationToken.None))
            .Returns(true);

        _commitWriter.Setup(item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>()))
            .ReturnsAsync(RecoveryState.Restored);

        _stateTransitions.Setup(item => item.ApplyExternalChangeDetected(session)).Returns(conflictedSession);
        _resultFactory.Setup(item => item.Conflict<TransactionCommitOutcome>(
            WorkspaceErrorCodes.TransactionConflicted,
            "Workspace inputs changed during commit promotion. Certification: Application. Detection source: FileSystemWatcher. Change kind: Renamed. Path: /workspace/Renamed.cs. Previous path: /workspace/Document.cs.",
            RequiredAction.RollbackTransaction,
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var selection = CreateSelection(session);
        var result = await _target.CommitAsync(selection, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _applicationCertification.Verify(item => item.Complete(
            session.InputManifest,
            It.Is<IEnumerable<string>>(paths =>
                paths.Contains(entry.TargetPath)
                && paths.Contains(entry.GetRequiredBackupPath())
                && paths.Contains(entry.GetRequiredStagedPath())
                && paths.Contains("/workspace/Created"))), Times.Once);

        _commitWriter.Verify(item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>()), Times.Once);
        _sessionStore.Verify(item => item.ReplaceSession(conflictedSession), Times.Once);
    }

    [Fact]
    public async Task GIVEN_AppliedStateDriftsAfterCertification_WHEN_Committing_THEN_ShouldRestoreAndConflict()
    {
        var session = CreateSession();
        var conflictedSession = session with { State = WorkspaceLifecycleState.TransactionConflicted };
        var manifest = CreateManifest();
        var plan = new WorkspaceCommitPlan(manifest, new Dictionary<string, ReadOnlyMemory<byte>>());
        var expected = CreateResult(WorkspaceOperationStatus.Conflict);
        SetupProtocol(session, plan);
        _commitWriter.SetupSequence(item => item.ValidateAppliedStateAsync(It.IsAny<WorkspaceCommitManifest>()))
            .ReturnsAsync(WorkspaceCommitValidationResult.Valid())
            .ReturnsAsync(WorkspaceCommitValidationResult.Invalid("Target changed."));

        _commitWriter.Setup(item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>()))
            .ReturnsAsync(RecoveryState.Restored);

        _stateTransitions.Setup(item => item.ApplyExternalChangeDetected(session)).Returns(conflictedSession);
        _resultFactory.Setup(item => item.Conflict<TransactionCommitOutcome>(
            WorkspaceErrorCodes.TransactionConflicted,
            It.IsAny<string>(),
            RequiredAction.RollbackTransaction,
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var selection = CreateSelection(session);
        var result = await _target.CommitAsync(selection, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _commitWriter.Verify(item => item.ValidateAppliedStateAsync(It.IsAny<WorkspaceCommitManifest>()), Times.Exactly(2));
        _commitWriter.Verify(item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>()), Times.Once);
        _sessionStore.Verify(item => item.ReplaceSession(conflictedSession), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ApplyRevalidationDetectsTargetDriftButRecoveryIsIncomplete_WHEN_Committing_THEN_ShouldRequireRecovery()
    {
        var session = CreateSession();
        var manifest = CreateManifest();
        var plan = new WorkspaceCommitPlan(manifest, new Dictionary<string, ReadOnlyMemory<byte>>());
        var expected = CreateResult(WorkspaceOperationStatus.Faulted);
        SetupProtocol(session, plan);
        _commitWriter.Setup(item => item.ApplyAsync(It.IsAny<WorkspaceCommitManifest>()))
            .ReturnsAsync(WorkspaceCommitValidationResult.Invalid("Target changed."));

        _commitWriter.Setup(item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>()))
            .ReturnsAsync(RecoveryState.RecoveryIncomplete);

        _resultFactory.Setup(item => item.Faulted<TransactionCommitOutcome>(
            "CommitFailed",
            It.IsAny<string>(),
            RequiredAction.ResolveRecovery,
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var result = await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _sessionStore.Verify(item => item.ReplaceSession(It.IsAny<WorkspaceSessionSnapshot>()), Times.Never);
        _recoveryStore.Verify(item => item.DeleteStatus(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("Plan")]
    [InlineData("Persist")]
    [InlineData("Revalidate")]
    [InlineData("ApplyingManifest")]
    public async Task GIVEN_PreApplicationIoFailure_WHEN_Committing_THEN_ShouldLeaveTargetsUnchangedAndReturnRetry(string failurePoint)
    {
        var session = CreateSession();
        var manifest = CreateManifest();
        var plan = new WorkspaceCommitPlan(manifest, new Dictionary<string, ReadOnlyMemory<byte>>());
        var expected = CreateResult(WorkspaceOperationStatus.Faulted);
        SetupProtocol(session, plan);
        _commitWriter.Setup(item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>())).ReturnsAsync(RecoveryState.Restored);
        switch (failurePoint)
        {
            case "Plan":
                _planner.Setup(item => item.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Solution>(), It.IsAny<Solution>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new IOException("plan"));

                break;

            case "Persist":
                _recoveryStore.Setup(item => item.PersistPlanAsync(plan, It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("persist"));
                break;

            case "Revalidate":
                _commitWriter.Setup(item => item.RevalidateAsync(manifest, It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("revalidate"));
                break;

            case "ApplyingManifest":
                _recoveryStore.Setup(item => item.WriteManifestAsync(
                    It.Is<WorkspaceCommitManifest>(value => value.State == RecoveryState.Applying),
                    It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("manifest"));

                break;
        }

        _resultFactory.Setup(item => item.Faulted<TransactionCommitOutcome>(
            "CommitPreparationFailed", It.IsAny<string>(), RequiredAction.Retry, It.IsAny<WorkspaceOperationContext>(), null, null)).Returns(expected);

        var result = await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _commitWriter.Verify(item => item.ApplyAsync(It.IsAny<WorkspaceCommitManifest>()), Times.Never);
        Times expectedRestorations;
        if (failurePoint == "Plan")
        {
            expectedRestorations = Times.Never();
        }
        else
        {
            expectedRestorations = Times.Once();
        }

        _commitWriter.Verify(
            item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>()),
            expectedRestorations);
    }

    [Fact]
    public async Task GIVEN_PreApplicationAccessFailure_WHEN_Committing_THEN_ShouldReturnRetryableFault()
    {
        var session = CreateSession();
        var manifest = CreateManifest();
        var plan = new WorkspaceCommitPlan(manifest, new Dictionary<string, ReadOnlyMemory<byte>>());
        var expected = CreateResult(WorkspaceOperationStatus.Faulted);
        SetupProtocol(session, plan);
        _planner.Setup(item => item.CreateAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Solution>(), It.IsAny<Solution>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("denied"));

        _resultFactory.Setup(item => item.Faulted<TransactionCommitOutcome>(
            "CommitPreparationFailed", It.IsAny<string>(), RequiredAction.Retry, It.IsAny<WorkspaceOperationContext>(), null, null)).Returns(expected);

        var result = await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _commitWriter.Verify(item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_PreApplicationFailureAndRecoveryStateCannotBePersisted_WHEN_Committing_THEN_ShouldReportDurableStateWarning()
    {
        var session = CreateSession();
        var manifest = CreateManifest();
        var plan = new WorkspaceCommitPlan(manifest, new Dictionary<string, ReadOnlyMemory<byte>>());
        var expected = CreateResult(WorkspaceOperationStatus.Faulted);
        SetupProtocol(session, plan);
        _commitWriter.Setup(item => item.RevalidateAsync(manifest, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("revalidate."));

        _commitWriter.Setup(item => item.RestoreAsync(manifest)).ReturnsAsync(RecoveryState.Restored);
        _recoveryStore.Setup(item => item.WriteManifestAsync(
            It.Is<WorkspaceCommitManifest>(value => value.State == RecoveryState.Restored),
            CancellationToken.None)).ThrowsAsync(new UnauthorizedAccessException("recovery manifest"));

        _resultFactory.Setup(item => item.Faulted<TransactionCommitOutcome>(
            "CommitPreparationFailed",
            "The transaction commit could not update its recovery record and no workspace changes were applied. Failure: revalidate. The final recovery state could not be persisted; any retained recovery record may report an earlier phase.",
            RequiredAction.Retry,
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var result = await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _recoveryStore.Verify(item => item.DeleteStatus("commit"), Times.Once);
    }

    [Fact]
    public async Task GIVEN_NonRecoverablePreparationFailure_WHEN_Committing_THEN_ShouldPropagateFailure()
    {
        var session = CreateSession();
        var manifest = CreateManifest();
        var plan = new WorkspaceCommitPlan(manifest, new Dictionary<string, ReadOnlyMemory<byte>>());
        SetupProtocol(session, plan);
        _planner.Setup(item => item.CreateAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Solution>(), It.IsAny<Solution>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("failed"));

        var action = async () => await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("failed");
        _commitWriter.Verify(item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_CommittedManifestWriteFailure_WHEN_Committing_THEN_ShouldRestoreBeforePublishingSession()
    {
        var session = CreateSession();
        var manifest = CreateManifest();
        var plan = new WorkspaceCommitPlan(manifest, new Dictionary<string, ReadOnlyMemory<byte>>());
        var expected = CreateResult(WorkspaceOperationStatus.Faulted);
        SetupProtocol(session, plan);
        _recoveryStore.Setup(item => item.WriteManifestAsync(
            It.Is<WorkspaceCommitManifest>(value => value.State == RecoveryState.Committed),
            CancellationToken.None)).ThrowsAsync(new IOException("committed"));

        _commitWriter.Setup(item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>())).ReturnsAsync(RecoveryState.Restored);
        _resultFactory.Setup(item => item.Faulted<TransactionCommitOutcome>(
            "CommitFailed", It.IsAny<string>(), RequiredAction.Retry, It.IsAny<WorkspaceOperationContext>(), null, null)).Returns(expected);

        var result = await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _sessionStore.Verify(item => item.TryCompleteTransaction(It.IsAny<WorkspaceSessionSnapshot>()), Times.Never);
        _commitWriter.Verify(item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_RestorationConflict_WHEN_ApplyFails_THEN_ShouldRetainRecoveryAndRequireResolution()
    {
        var session = CreateSession();
        var manifest = CreateManifest();
        var plan = new WorkspaceCommitPlan(manifest, new Dictionary<string, ReadOnlyMemory<byte>>());
        var expected = CreateResult(WorkspaceOperationStatus.Faulted);
        SetupProtocol(session, plan);
        _commitWriter.Setup(item => item.ApplyAsync(It.IsAny<WorkspaceCommitManifest>())).ThrowsAsync(new IOException("apply."));
        _commitWriter.Setup(item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>())).ReturnsAsync(RecoveryState.RecoveryConflict);
        _recoveryStore.Setup(item => item.WriteManifestAsync(
            It.Is<WorkspaceCommitManifest>(value => value.State == RecoveryState.RecoveryConflict),
            CancellationToken.None)).ThrowsAsync(new IOException("recovery manifest"));

        _resultFactory.Setup(item => item.Faulted<TransactionCommitOutcome>(
            "CommitFailed",
            "The transaction commit failed and its changes were restored or retained for recovery. Failure: apply. The final recovery state could not be persisted; any retained recovery record may report an earlier phase.",
            RequiredAction.ResolveRecovery,
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var result = await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _recoveryStore.Verify(item => item.WriteManifestAsync(
            It.Is<WorkspaceCommitManifest>(value => value.State == RecoveryState.RecoveryConflict),
            CancellationToken.None), Times.Once);

        _recoveryStore.Verify(item => item.DeleteStatus(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_TerminalCleanupFailure_WHEN_Committing_THEN_ShouldSucceedAndRetainCommittedRecoveryEvidence()
    {
        var session = CreateSession();
        var manifest = CreateManifest();
        var plan = new WorkspaceCommitPlan(manifest, new Dictionary<string, ReadOnlyMemory<byte>>());
        var expected = CreateResult(WorkspaceOperationStatus.Succeeded);
        SetupProtocol(session, plan);
        _commitWriter.Setup(item => item.CompleteAsync(It.IsAny<WorkspaceCommitManifest>())).ReturnsAsync(false);
        _resultFactory.Setup(item => item.Succeeded(
            It.Is<TransactionCommitOutcome>(outcome => outcome.Committed),
            It.IsAny<WorkspaceOperationContext>(), null, null)).Returns(expected);

        var result = await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _sessionStore.Verify(item => item.TryCompleteTransaction(It.IsAny<WorkspaceSessionSnapshot>()), Times.Once);
        _recoveryStore.Verify(item => item.DeleteStatus(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_TransactionOwnershipChangesDuringCompletion_WHEN_Committing_THEN_ShouldRestoreAndReturnFaultWithoutContinuation()
    {
        var session = CreateSession();
        var manifest = CreateManifest();
        var plan = new WorkspaceCommitPlan(manifest, new Dictionary<string, ReadOnlyMemory<byte>>());
        var expected = CreateResult(WorkspaceOperationStatus.Faulted);
        SetupProtocol(session, plan);
        _sessionStore.Setup(item => item.TryCompleteTransaction(It.IsAny<WorkspaceSessionSnapshot>()))
            .Returns(TransactionCompletionResult.OwnershipChanged(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Guid.Parse("22222222-2222-2222-2222-222222222222")));

        _commitWriter.Setup(item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>()))
            .ReturnsAsync(RecoveryState.Restored);

        _resultFactory.Setup(item => item.Faulted<TransactionCommitOutcome>(
            "CommitFailed",
            It.Is<string>(message => message.Contains("Restart the server before continuing.", StringComparison.Ordinal)),
            null,
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var result = await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _sessionStore.Verify(item => item.TryCompleteTransaction(It.IsAny<WorkspaceSessionSnapshot>()), Times.Once);
        _commitWriter.Verify(item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>()), Times.Once);
        _recoveryStore.Verify(item => item.DeleteStatus(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_CancellationDuringPreparation_WHEN_Committing_THEN_ShouldCleanPreparedArtifactsAndPropagateCancellation()
    {
        var session = CreateSession();
        var manifest = CreateManifest();
        var plan = new WorkspaceCommitPlan(manifest, new Dictionary<string, ReadOnlyMemory<byte>>());
        SetupProtocol(session, plan);
        _recoveryStore.Setup(item => item.PersistPlanAsync(plan, It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());
        _commitWriter.Setup(item => item.RestoreAsync(manifest)).ReturnsAsync(RecoveryState.Restored);

        var action = async () => await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<OperationCanceledException>();
        _recoveryStore.Verify(item => item.DeleteStatus("commit"), Times.Once);
    }

    [Fact]
    public async Task GIVEN_CancellationDuringPlanning_WHEN_Committing_THEN_ShouldPropagateWithoutRestoration()
    {
        var session = CreateSession();
        _sessionStore.Setup(item => item.ReadSession(Guid.Parse("11111111-1111-1111-1111-111111111111"))).Returns(session);
        _lockManager.Setup(item => item.Acquire("/workspace")).Returns(CreateLockAcquisition(lockAvailable: true));
        _planner.Setup(item => item.CreateAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Solution>(),
            It.IsAny<Solution>(),
            It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());

        var action = async () => await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<OperationCanceledException>();
        _commitWriter.Verify(item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>()), Times.Never);
    }

    public void Dispose()
    {
        _applicationInputManifest.Dispose();
        _workspace.Dispose();
    }

    private WorkspaceSessionSnapshot CreateSession()
    {
        var baseline = _workspace.CurrentSolution;
        var current = baseline.AddProject("Project", "Project", LanguageNames.CSharp).Solution;
        var committedSnapshotId = new WorkspaceSnapshotId(1);
        var transaction = new WorkspaceTransaction
        {
            TransactionId = new WorkspaceTransactionId(1),
            BaselineSnapshotId = committedSnapshotId,
            BaselineSolution = baseline,
            Revisions =
            [
                new WorkspaceTransactionRevision
                {
                    SnapshotId = new WorkspaceSnapshotId(2),
                    Solution = current,
                    Changes = new ChangeSummary(),
                    Operation = "Operation",
                    Summary = "Summary",
                    Preview = new MutationPreview(),
                },
            ],
            CurrentRevision = 1,
            MaxRevisions = 3,
        };

        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            LoadedPath = "/workspace/solution.slnx",
            WorkspaceRoot = "/workspace",
        };

        return new WorkspaceSessionSnapshot
        {
            CommittedSnapshotId = committedSnapshotId,
            State = WorkspaceLifecycleState.TransactionActive,
            Workspace = workspaceIdentity,
            LoadedWorkspace = new Mock<ILoadedWorkspace>().Object,
            CurrentSolution = current,
            Transaction = transaction,
            InputManifest = new WorkspaceInputManifest(),
            OperationGate = new Mock<IWorkspaceOperationGate>().Object,
            CurrentSnapshotIdentity = WorkspaceSnapshotIdentity.Create(
                workspaceIdentity,
                committedSnapshotId,
                transaction),
        };
    }

    private static WorkspaceSelection CreateSelection(WorkspaceSessionSnapshot session)
    {
        return new()
        {
            WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Session = session,
        };
    }

    private static WorkspaceCommitManifest CreateManifest()
    {
        return new()
        {
            CommitId = "commit",
            LoadedPath = "/workspace/solution.slnx",
            WorkspaceRoot = "/workspace",
            State = RecoveryState.Prepared,
            Entries = [],
            CreatedDirectories = [],
        };
    }

    private static WorkspaceOperationResult<TransactionCommitOutcome> CreateResult(WorkspaceOperationStatus status)
    {
        if (status == WorkspaceOperationStatus.Succeeded)
        {
            var outcome = new TransactionCommitOutcome();
            return WorkspaceOperationResult.Succeeded(outcome);
        }

        if (status == WorkspaceOperationStatus.NoChange)
        {
            return WorkspaceOperationResult.NoChange<TransactionCommitOutcome>();
        }

        var error = new WorkspaceOperationError { Code = "Code", Message = "Message" };

        return status switch
        {
            WorkspaceOperationStatus.Rejected => WorkspaceOperationResult.Rejected<TransactionCommitOutcome>(error),
            WorkspaceOperationStatus.Conflict => WorkspaceOperationResult.Conflict<TransactionCommitOutcome>(error),
            WorkspaceOperationStatus.Faulted => WorkspaceOperationResult.Faulted<TransactionCommitOutcome>(error),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "A supported workspace status is required."),
        };
    }

    private static WorkspaceCommitLockAcquisition CreateLockAcquisition(bool lockAvailable)
    {
        if (!lockAvailable)
        {
            return WorkspaceCommitLockAcquisition.Contended();
        }

        var commitLock = new Mock<IWorkspaceCommitLock>();
        return WorkspaceCommitLockAcquisition.Acquired(commitLock.Object);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The manifest is transferred through the change-detector mock to the commit service, which either disposes it or installs it as session-owned state.")]
    private void SetupProtocol(WorkspaceSessionSnapshot session, WorkspaceCommitPlan plan)
    {
        _sessionStore.Setup(item => item.ReadSession(Guid.Parse("11111111-1111-1111-1111-111111111111"))).Returns(session);
        _lockManager.Setup(item => item.Acquire(It.IsAny<string>())).Returns(CreateLockAcquisition(lockAvailable: true));
        _planner.Setup(item => item.CreateAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Solution>(), It.IsAny<Solution>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkspaceCommitPlanResult.Succeeded(plan));

        _commitWriter.Setup(item => item.RevalidateAsync(It.IsAny<WorkspaceCommitManifest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkspaceCommitValidationResult.Valid());

        _commitWriter.Setup(item => item.ApplyAsync(It.IsAny<WorkspaceCommitManifest>()))
            .ReturnsAsync(WorkspaceCommitValidationResult.Valid());

        _changeDetector
            .Setup(item => item.BuildManifest(
                It.IsAny<Solution>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                _promotionCertification.Object))
            .Returns(new WorkspaceInputManifest());

        _stateTransitions.Setup(item => item.Fire(It.IsAny<WorkspaceLifecycleState>(), WorkspaceTrigger.TransactionCommitted)).Returns(WorkspaceLifecycleState.Ready);
    }
}
