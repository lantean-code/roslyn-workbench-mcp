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
    public async Task GIVEN_NoActiveTransaction_WHEN_Committing_THEN_ShouldRequireTransaction()
    {
        var session = CreateSession() with { Transaction = null };
        var expected = CreateResult(WorkspaceOperationStatus.Rejected);
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
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
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns((WorkspaceSessionSnapshot?)null);
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
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
        _snapshotGuard.Setup(item => item.Validate(session, It.IsAny<SnapshotPrecondition?>())).Returns(mismatch);
        _resultFactory.Setup(item => item.Conflict<TransactionCommitOutcome>(mismatch, It.IsAny<WorkspaceOperationContext>(), null, null)).Returns(expected);

        var result = await _target.CommitAsync(CreateSelection(session), new SnapshotPrecondition(), TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _lockManager.Verify(item => item.Acquire(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ConflictedTransaction_WHEN_Committing_THEN_ShouldRequireRollback()
    {
        var session = CreateSession() with { State = WorkspaceLifecycleState.TransactionConflicted };
        var expected = CreateResult(WorkspaceOperationStatus.Conflict);
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
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
        session = session with { Transaction = empty, CurrentSolution = empty.BaselineSolution };
        var expected = CreateResult(WorkspaceOperationStatus.NoChange);
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
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
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
        _changeDetector.Setup(item => item.HasChanged(session.InputManifest, It.IsAny<CancellationToken>())).Returns(true);
        _stateTransitions.Setup(item => item.ApplyExternalChangeDetected(session)).Returns(conflicted);
        _resultFactory.Setup(item => item.Conflict<TransactionCommitOutcome>(
            WorkspaceErrorCodes.TransactionConflicted, It.IsAny<string>(), RequiredAction.RollbackTransaction,
            It.IsAny<WorkspaceOperationContext>(), null, null)).Returns(expected);

        var result = await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _sessionStore.Verify(item => item.ReplaceSession(conflicted), Times.Once);
    }

    [Fact]
    public async Task GIVEN_CommitLockContention_WHEN_Committing_THEN_ShouldReturnRetryableWorkspaceBusy()
    {
        var session = CreateSession();
        var expected = CreateResult(WorkspaceOperationStatus.Rejected);
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
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
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
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
        var session = CreateSession();
        var transaction = session.Transaction!;
        var commitLock = new Mock<IWorkspaceCommitLock>();
        var manifest = CreateManifest();
        var plan = new WorkspaceCommitPlan(manifest, new Dictionary<string, ReadOnlyMemory<byte>>());
        var expected = CreateResult(WorkspaceOperationStatus.Succeeded);
        var inputManifest = new WorkspaceInputManifest();
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
        _lockManager.Setup(item => item.Acquire("/workspace")).Returns(WorkspaceCommitLockAcquisition.Acquired(commitLock.Object));
        _planner.Setup(item => item.CreateAsync(
            It.IsAny<string>(), "/workspace/solution.slnx", "/workspace", transaction.BaselineSolution, transaction.CurrentSolution, TestContext.Current.CancellationToken))
            .ReturnsAsync(WorkspaceCommitPlanResult.Succeeded(plan));
        _commitWriter.Setup(item => item.RevalidateAsync(manifest, TestContext.Current.CancellationToken))
            .ReturnsAsync(WorkspaceCommitValidationResult.Valid());
        _commitWriter.Setup(item => item.ApplyAsync(It.IsAny<WorkspaceCommitManifest>()))
            .ReturnsAsync(WorkspaceCommitValidationResult.Valid());
        _changeDetector.Setup(item => item.BuildManifest(transaction.CurrentSolution, "/workspace/solution.slnx")).Returns(inputManifest);
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
        _sessionStore.Verify(item => item.ReplaceSessionAndSetTransactionOwner(
            It.Is<WorkspaceSessionSnapshot>(value => value.Transaction == null && value.CurrentSolution == transaction.CurrentSolution && value.InputManifest == inputManifest), null), Times.Once);
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
        var inputManifest = new WorkspaceInputManifest
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
        _changeDetector.Setup(item => item.BuildManifest(transaction.CurrentSolution, "/workspace/solution.slnx"))
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
        _sessionStore.Verify(item => item.ReplaceSessionAndSetTransactionOwner(
            It.Is<WorkspaceSessionSnapshot>(value =>
                value.State == WorkspaceLifecycleState.WorkspaceOutOfDate
                && value.Transaction == null
                && value.CurrentSolution == transaction.CurrentSolution
                && value.InputManifest == inputManifest
                && value.LoadDiagnostics.Count == 1
                && value.LoadDiagnostics[0].Id == "WorkspaceInputEvaluationFailed"),
            null), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ApplyFailure_WHEN_Committing_THEN_ShouldRestoreNonCancellablyAndReturnRetryableFault()
    {
        var session = CreateSession();
        var manifest = CreateManifest();
        var plan = new WorkspaceCommitPlan(manifest, new Dictionary<string, ReadOnlyMemory<byte>>());
        var expected = CreateResult(WorkspaceOperationStatus.Faulted);
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
        _lockManager.Setup(item => item.Acquire(It.IsAny<string>())).Returns(CreateLockAcquisition(lockAvailable: true));
        _planner.Setup(item => item.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Solution>(), It.IsAny<Solution>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkspaceCommitPlanResult.Succeeded(plan));
        _commitWriter.Setup(item => item.RevalidateAsync(manifest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkspaceCommitValidationResult.Valid());
        _commitWriter.Setup(item => item.ApplyAsync(It.IsAny<WorkspaceCommitManifest>())).ThrowsAsync(new IOException("failure"));
        _commitWriter.Setup(item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>())).ReturnsAsync(RecoveryState.Restored);
        _resultFactory.Setup(item => item.Faulted<TransactionCommitOutcome>(
            "CommitFailed", It.IsAny<string>(), RequiredAction.Retry, It.IsAny<WorkspaceOperationContext>(), null, null)).Returns(expected);

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
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
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

        var result = await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

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

        var result = await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

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

        var result = await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

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
        _commitWriter.Verify(
            item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>()),
            failurePoint == "Plan" ? Times.Never() : Times.Once());
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
            .ThrowsAsync(new IOException("revalidate"));
        _commitWriter.Setup(item => item.RestoreAsync(manifest)).ReturnsAsync(RecoveryState.Restored);
        _recoveryStore.Setup(item => item.WriteManifestAsync(
            It.Is<WorkspaceCommitManifest>(value => value.State == RecoveryState.Restored),
            CancellationToken.None)).ThrowsAsync(new UnauthorizedAccessException("recovery manifest"));
        _resultFactory.Setup(item => item.Faulted<TransactionCommitOutcome>(
            "CommitPreparationFailed",
            "The transaction commit could not update its recovery record and no workspace changes were applied. The final recovery state could not be persisted; any retained recovery record may report an earlier phase.",
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
        _sessionStore.Verify(item => item.ReplaceSessionAndSetTransactionOwner(It.IsAny<WorkspaceSessionSnapshot>(), It.IsAny<string?>()), Times.Never);
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
        _commitWriter.Setup(item => item.ApplyAsync(It.IsAny<WorkspaceCommitManifest>())).ThrowsAsync(new IOException("apply"));
        _commitWriter.Setup(item => item.RestoreAsync(It.IsAny<WorkspaceCommitManifest>())).ReturnsAsync(RecoveryState.RecoveryConflict);
        _recoveryStore.Setup(item => item.WriteManifestAsync(
            It.Is<WorkspaceCommitManifest>(value => value.State == RecoveryState.RecoveryConflict),
            CancellationToken.None)).ThrowsAsync(new IOException("recovery manifest"));
        _resultFactory.Setup(item => item.Faulted<TransactionCommitOutcome>(
            "CommitFailed",
            "The transaction commit failed and its changes were restored or retained for recovery. The final recovery state could not be persisted; any retained recovery record may report an earlier phase.",
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
        _sessionStore.Verify(item => item.ReplaceSessionAndSetTransactionOwner(It.IsAny<WorkspaceSessionSnapshot>(), null), Times.Once);
        _recoveryStore.Verify(item => item.DeleteStatus(It.IsAny<string>()), Times.Never);
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
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
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
        _workspace.Dispose();
    }

    private WorkspaceSessionSnapshot CreateSession()
    {
        var baseline = _workspace.CurrentSolution;
        var current = baseline.AddProject("Project", "Project", LanguageNames.CSharp).Solution;
        var transaction = new WorkspaceTransaction
        {
            BaselineSolution = baseline,
            Revisions = [new WorkspaceTransactionRevision { Solution = current }],
            CurrentRevision = 1,
            MaxRevisions = 3,
        };
        return new WorkspaceSessionSnapshot
        {
            State = WorkspaceLifecycleState.TransactionActive,
            Workspace = new WorkspaceIdentity
            {
                WorkspaceId = "WorkspaceId",
                LoadedPath = "/workspace/solution.slnx",
                WorkspaceRoot = "/workspace",
            },
            LoadedWorkspace = new Mock<ILoadedWorkspace>().Object,
            CurrentSolution = current,
            Transaction = transaction,
            InputManifest = new WorkspaceInputManifest(),
            OperationGate = new Mock<IWorkspaceOperationGate>().Object,
        };
    }

    private static WorkspaceSelection CreateSelection(WorkspaceSessionSnapshot session)
    {
        return new()
        {
            WorkspaceId = "WorkspaceId",
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
        return new() { Status = status };
    }

    private static WorkspaceCommitLockAcquisition CreateLockAcquisition(bool lockAvailable)
    {
        return lockAvailable
            ? WorkspaceCommitLockAcquisition.Acquired(new Mock<IWorkspaceCommitLock>().Object)
            : WorkspaceCommitLockAcquisition.Contended();
    }

    private void SetupProtocol(WorkspaceSessionSnapshot session, WorkspaceCommitPlan plan)
    {
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
        _lockManager.Setup(item => item.Acquire(It.IsAny<string>())).Returns(CreateLockAcquisition(lockAvailable: true));
        _planner.Setup(item => item.CreateAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Solution>(), It.IsAny<Solution>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkspaceCommitPlanResult.Succeeded(plan));
        _commitWriter.Setup(item => item.RevalidateAsync(It.IsAny<WorkspaceCommitManifest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkspaceCommitValidationResult.Valid());
        _commitWriter.Setup(item => item.ApplyAsync(It.IsAny<WorkspaceCommitManifest>()))
            .ReturnsAsync(WorkspaceCommitValidationResult.Valid());
        _changeDetector.Setup(item => item.BuildManifest(It.IsAny<Solution>(), It.IsAny<string>())).Returns(new WorkspaceInputManifest());
        _stateTransitions.Setup(item => item.Fire(It.IsAny<WorkspaceLifecycleState>(), WorkspaceTrigger.TransactionCommitted)).Returns(WorkspaceLifecycleState.Ready);
    }
}
