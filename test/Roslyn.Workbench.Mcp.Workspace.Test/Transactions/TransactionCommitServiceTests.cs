using Microsoft.Extensions.Options;

using Roslyn.Workbench.Mcp.Workspace.ChangeDetection;
using Roslyn.Workbench.Mcp.Workspace.Configuration;
using Roslyn.Workbench.Mcp.Workspace.Loading;
using Roslyn.Workbench.Mcp.Workspace.Recovery;
using Roslyn.Workbench.Mcp.Workspace.Selection;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class TransactionCommitServiceTests : IDisposable
{
    private readonly AdhocWorkspace _workspace;
    private readonly Mock<IWorkspaceSessionStore> _sessionStore;
    private readonly Mock<IWorkspaceChangeDetector> _changeDetector;
    private readonly Mock<IWorkspaceStateTransitions> _stateTransitions;
    private readonly Mock<ISnapshotGuard> _snapshotGuard;
    private readonly Mock<IWorkspaceOperationResultFactory> _resultFactory;
    private readonly Mock<ICommitRecoveryStore> _recoveryStore;
    private readonly Mock<IWorkspaceCommitWriter> _commitWriter;
    private readonly Mock<ILoadedWorkspace> _loadedWorkspace;
    private readonly TransactionCommitService _target;

    public TransactionCommitServiceTests()
    {
        _workspace = new AdhocWorkspace();
        _sessionStore = new Mock<IWorkspaceSessionStore>();
        _changeDetector = new Mock<IWorkspaceChangeDetector>();
        _stateTransitions = new Mock<IWorkspaceStateTransitions>();
        _snapshotGuard = new Mock<ISnapshotGuard>();
        _resultFactory = new Mock<IWorkspaceOperationResultFactory>();
        _recoveryStore = new Mock<ICommitRecoveryStore>();
        _commitWriter = new Mock<IWorkspaceCommitWriter>();
        _loadedWorkspace = new Mock<ILoadedWorkspace>();
        _target = new TransactionCommitService(
            _sessionStore.Object,
            _changeDetector.Object,
            _stateTransitions.Object,
            _snapshotGuard.Object,
            _resultFactory.Object,
            _recoveryStore.Object,
            _commitWriter.Object);
    }

    [Fact]
    public async Task GIVEN_NullSelection_WHEN_Committing_THEN_ShouldThrowArgumentNullException()
    {
        var action = async () => await _target.CommitAsync(
            null!,
            null,
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_Committing_THEN_ShouldPropagateCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var action = async () => await _target.CommitAsync(
            CreateSelection(CreateSession(transaction: null)),
            null,
            cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_MissingSession_WHEN_Committing_THEN_ShouldRequireTransaction()
    {
        var selection = CreateSelection(CreateSession(transaction: null));
        var expected = CreateResult<TransactionCommitOutcome>();
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns((WorkspaceSessionSnapshot?)null);
        SetupRejectedResult(expected, WorkspaceErrorCodes.TransactionRequired);

        var result = await _target.CommitAsync(selection, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_MissingTransaction_WHEN_Committing_THEN_ShouldRequireTransaction()
    {
        var session = CreateSession(transaction: null);
        var expected = CreateResult<TransactionCommitOutcome>();
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
        SetupRejectedResult(expected, WorkspaceErrorCodes.TransactionRequired);

        var result = await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_SnapshotMismatch_WHEN_Committing_THEN_ShouldReturnConflict()
    {
        var session = CreateSession(CreateTransaction(currentRevision: 1));
        var mismatch = new WorkspaceOperationError { Code = "SnapshotMismatch", Message = "Message" };
        var expected = CreateResult<TransactionCommitOutcome>();
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
        _snapshotGuard.Setup(item => item.Validate(session, It.IsAny<SnapshotPrecondition?>())).Returns(mismatch);
        _resultFactory.Setup(item => item.Conflict<TransactionCommitOutcome>(
            mismatch,
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var result = await _target.CommitAsync(
            CreateSelection(session),
            new SnapshotPrecondition(),
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_ConflictedTransaction_WHEN_Committing_THEN_ShouldRequireRollback()
    {
        var session = CreateSession(CreateTransaction(currentRevision: 1)) with
        {
            State = WorkspaceLifecycleState.TransactionConflicted,
        };
        var expected = CreateResult<TransactionCommitOutcome>();
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
        SetupConflictResult(expected, WorkspaceErrorCodes.TransactionConflicted);

        var result = await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_ZeroRevisionTransaction_WHEN_Committing_THEN_ShouldReturnNoChange()
    {
        var transaction = CreateTransaction(currentRevision: 0);
        var session = CreateSession(transaction);
        var expected = CreateResult<TransactionCommitOutcome>();
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
        _resultFactory.Setup(item => item.NoChange(
            It.IsAny<WorkspaceOperationContext>(),
            It.Is<TransactionCommitOutcome>(outcome => !outcome.Committed && outcome.Transaction!.Revision == 0),
            null,
            null)).Returns(expected);

        var result = await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _commitWriter.Verify(item => item.ApplyAsync(
            It.IsAny<Solution>(),
            It.IsAny<Solution>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ExternalWorkspaceChange_WHEN_Committing_THEN_ShouldTransitionAndReturnConflict()
    {
        var session = CreateSession(CreateTransaction(currentRevision: 1));
        var conflictedSession = session with { State = WorkspaceLifecycleState.TransactionConflicted };
        var expected = CreateResult<TransactionCommitOutcome>();
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
        _changeDetector.Setup(item => item.HasChanged(session.InputManifest, TestContext.Current.CancellationToken)).Returns(true);
        _stateTransitions.Setup(item => item.ApplyExternalChangeDetected(session)).Returns(conflictedSession);
        SetupConflictResult(expected, WorkspaceErrorCodes.TransactionConflicted);

        var result = await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _sessionStore.Verify(item => item.ReplaceSession(conflictedSession), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ValidTransaction_WHEN_Committing_THEN_ShouldApplyAndCompleteRecoverySequence()
    {
        var transaction = CreateTransaction(currentRevision: 1);
        var session = CreateSession(transaction);
        var manifest = new WorkspaceInputManifest();
        var expected = CreateResult<TransactionCommitOutcome>();
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
        _changeDetector.Setup(item => item.HasChanged(session.InputManifest, TestContext.Current.CancellationToken)).Returns(false);
        _changeDetector.Setup(item => item.BuildManifest(transaction.CurrentSolution, "LoadedPath")).Returns(manifest);
        _stateTransitions.Setup(item => item.Fire(
            WorkspaceLifecycleState.TransactionActive,
            WorkspaceTrigger.TransactionCommitted)).Returns(WorkspaceLifecycleState.Ready);
        _resultFactory.Setup(item => item.Succeeded(
            It.Is<TransactionCommitOutcome>(outcome => outcome.Committed),
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var result = await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _recoveryStore.Verify(item => item.WriteStatusAsync(
            It.Is<RecoveryStatus>(status => status.State == RecoveryState.Prepared),
            TestContext.Current.CancellationToken), Times.Once);
        _recoveryStore.Verify(item => item.WriteStatusAsync(
            It.Is<RecoveryStatus>(status => status.State == RecoveryState.Applying),
            TestContext.Current.CancellationToken), Times.Once);
        _commitWriter.Verify(item => item.ApplyAsync(
            transaction.BaselineSolution,
            transaction.CurrentSolution,
            TestContext.Current.CancellationToken), Times.Once);
        _loadedWorkspace.Verify(item => item.ApplyChanges(transaction.CurrentSolution), Times.Once);
        _sessionStore.Verify(item => item.ReplaceSessionAndSetTransactionOwner(
            It.Is<WorkspaceSessionSnapshot>(committed =>
                committed.Transaction == null
                && committed.CurrentSolution == transaction.CurrentSolution
                && committed.InputManifest == manifest
                && committed.State == WorkspaceLifecycleState.Ready),
            null), Times.Once);
        _recoveryStore.Verify(item => item.DeleteStatus(It.IsAny<string>()), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GIVEN_InitialRecoveryWriteFailure_WHEN_Committing_THEN_ShouldReturnRetryablePreparationFailure(bool isIoException)
    {
        var transaction = CreateTransaction(currentRevision: 1);
        var session = CreateSession(transaction);
        var expected = CreateResult<TransactionCommitOutcome>();
        var exception = isIoException
            ? (Exception)new IOException("FailureMessage")
            : new UnauthorizedAccessException("FailureMessage");
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
        _recoveryStore
            .Setup(item => item.WriteStatusAsync(
                It.Is<RecoveryStatus>(status => status.State == RecoveryState.Prepared),
                TestContext.Current.CancellationToken))
            .ThrowsAsync(exception);
        _resultFactory.Setup(item => item.Faulted<TransactionCommitOutcome>(
            "CommitPreparationFailed",
            It.Is<string>(message => message.Contains("no workspace changes were applied", StringComparison.Ordinal)),
            RequiredAction.Retry,
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var result = await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _commitWriter.Verify(item => item.ApplyAsync(
            It.IsAny<Solution>(),
            It.IsAny<Solution>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _recoveryStore.Verify(item => item.WriteStatusAsync(
            It.Is<RecoveryStatus>(status => status.State == RecoveryState.RecoveryIncomplete),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GIVEN_ApplyingRecoveryWriteFailure_WHEN_Committing_THEN_ShouldReportPreparationFailureRequiringRecovery(bool isIoException)
    {
        var transaction = CreateTransaction(currentRevision: 1);
        var session = CreateSession(transaction);
        var expected = CreateResult<TransactionCommitOutcome>();
        var exception = isIoException
            ? (Exception)new IOException("FailureMessage")
            : new UnauthorizedAccessException("FailureMessage");
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
        _recoveryStore
            .Setup(item => item.WriteStatusAsync(
                It.Is<RecoveryStatus>(status => status.State == RecoveryState.Applying),
                TestContext.Current.CancellationToken))
            .ThrowsAsync(exception);
        _resultFactory.Setup(item => item.Faulted<TransactionCommitOutcome>(
            "CommitPreparationFailed",
            It.Is<string>(message => message.Contains("no workspace changes were applied", StringComparison.Ordinal)),
            RequiredAction.ResolveRecovery,
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var result = await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _commitWriter.Verify(item => item.ApplyAsync(
            It.IsAny<Solution>(),
            It.IsAny<Solution>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _recoveryStore.Verify(item => item.WriteStatusAsync(
            It.Is<RecoveryStatus>(status => status.State == RecoveryState.RecoveryIncomplete && status.Message == "FailureMessage"),
            TestContext.Current.CancellationToken), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GIVEN_RecoverableWriterFailure_WHEN_Committing_THEN_ShouldRecordIncompleteRecovery(bool isIoException)
    {
        var transaction = CreateTransaction(currentRevision: 1);
        var session = CreateSession(transaction);
        var expected = CreateResult<TransactionCommitOutcome>();
        var exception = isIoException
            ? (Exception)new IOException("FailureMessage")
            : new UnauthorizedAccessException("FailureMessage");
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
        _commitWriter.Setup(item => item.ApplyAsync(
            transaction.BaselineSolution,
            transaction.CurrentSolution,
            TestContext.Current.CancellationToken)).ThrowsAsync(exception);
        _resultFactory.Setup(item => item.Faulted<TransactionCommitOutcome>(
            "CommitFailed",
            It.IsAny<string>(),
            RequiredAction.ResolveRecovery,
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var result = await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _recoveryStore.Verify(item => item.WriteStatusAsync(
            It.Is<RecoveryStatus>(status =>
                status.State == RecoveryState.RecoveryIncomplete && status.Message == "FailureMessage"),
            TestContext.Current.CancellationToken), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GIVEN_RecoveryRecordFailureAfterWriterFailure_WHEN_Committing_THEN_ShouldPreservePrimaryCommitFailure(bool isIoException)
    {
        var transaction = CreateTransaction(currentRevision: 1);
        var session = CreateSession(transaction);
        var expected = CreateResult<TransactionCommitOutcome>();
        var recoveryException = isIoException
            ? (Exception)new IOException("RecoveryFailure")
            : new UnauthorizedAccessException("RecoveryFailure");
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
        _commitWriter.Setup(item => item.ApplyAsync(
            transaction.BaselineSolution,
            transaction.CurrentSolution,
            TestContext.Current.CancellationToken)).ThrowsAsync(new IOException("PrimaryFailure"));
        _recoveryStore
            .Setup(item => item.WriteStatusAsync(
                It.Is<RecoveryStatus>(status => status.State == RecoveryState.RecoveryIncomplete),
                TestContext.Current.CancellationToken))
            .ThrowsAsync(recoveryException);
        _resultFactory.Setup(item => item.Faulted<TransactionCommitOutcome>(
            "CommitFailed",
            It.Is<string>(message => message.Contains("partially applied", StringComparison.Ordinal)),
            RequiredAction.ResolveRecovery,
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var result = await _target.CommitAsync(CreateSelection(session), null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _recoveryStore.Verify(item => item.WriteStatusAsync(
            It.Is<RecoveryStatus>(status => status.State == RecoveryState.RecoveryIncomplete && status.Message == "PrimaryFailure"),
            TestContext.Current.CancellationToken), Times.Once);
    }

    [Fact]
    public async Task GIVEN_CancellationDuringWrite_WHEN_Committing_THEN_ShouldPropagateCancellation()
    {
        var transaction = CreateTransaction(currentRevision: 1);
        var session = CreateSession(transaction);
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
        _commitWriter.Setup(item => item.ApplyAsync(
            transaction.BaselineSolution,
            transaction.CurrentSolution,
            TestContext.Current.CancellationToken)).ThrowsAsync(new OperationCanceledException());

        var action = async () => await _target.CommitAsync(
            CreateSelection(session),
            null,
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    private WorkspaceSessionSnapshot CreateSession(WorkspaceTransaction? transaction)
    {
        return new WorkspaceSessionSnapshot
        {
            State = transaction is null ? WorkspaceLifecycleState.Ready : WorkspaceLifecycleState.TransactionActive,
            Workspace = new WorkspaceIdentity
            {
                WorkspaceId = "WorkspaceId",
                WorkspaceEpoch = 2,
                LoadedPath = "LoadedPath",
            },
            LoadedWorkspace = _loadedWorkspace.Object,
            CurrentSolution = transaction?.CurrentSolution ?? _workspace.CurrentSolution,
            Transaction = transaction,
            InputManifest = new WorkspaceInputManifest(),
            OperationGate = new Mock<IWorkspaceOperationGate>().Object,
        };
    }

    private WorkspaceTransaction CreateTransaction(int currentRevision)
    {
        var baselineSolution = _workspace.CurrentSolution;
        var currentSolution = baselineSolution.AddProject("Project", "Project", LanguageNames.CSharp).Solution;
        return new WorkspaceTransaction
        {
            BaselineSolution = baselineSolution,
            Revisions = [new WorkspaceTransactionRevision { Solution = currentSolution }],
            CurrentRevision = currentRevision,
            MaxRevisions = 3,
        };
    }

    private static WorkspaceSelection CreateSelection(WorkspaceSessionSnapshot session)
    {
        return new WorkspaceSelection
        {
            WorkspaceId = "WorkspaceId",
            Session = session,
        };
    }

    private void SetupRejectedResult(WorkspaceOperationResult<TransactionCommitOutcome> result, string code)
    {
        _resultFactory.Setup(item => item.Rejected<TransactionCommitOutcome>(
            code,
            It.IsAny<string>(),
            It.IsAny<RequiredAction?>(),
            It.IsAny<WorkspaceOperationContext?>(),
            null,
            null)).Returns(result);
    }

    private void SetupConflictResult(WorkspaceOperationResult<TransactionCommitOutcome> result, string code)
    {
        _resultFactory.Setup(item => item.Conflict<TransactionCommitOutcome>(
            code,
            It.IsAny<string>(),
            It.IsAny<RequiredAction?>(),
            It.IsAny<WorkspaceOperationContext?>(),
            null,
            null)).Returns(result);
    }

    private static WorkspaceOperationResult<TOutcome> CreateResult<TOutcome>()
    {
        return new WorkspaceOperationResult<TOutcome>();
    }
}
