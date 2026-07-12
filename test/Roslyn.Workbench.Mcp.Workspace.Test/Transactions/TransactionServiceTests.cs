using Microsoft.Extensions.Options;

using Roslyn.Workbench.Mcp.Workspace.Configuration;
using Roslyn.Workbench.Mcp.Workspace.Coordination;
using Roslyn.Workbench.Mcp.Workspace.Selection;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class TransactionServiceTests : IDisposable
{
    private readonly AdhocWorkspace _workspace;
    private readonly Mock<IWorkspaceSessionStore> _sessionStore;
    private readonly Mock<IWorkspaceSessionAcquirer> _sessionAcquirer;
    private readonly Mock<IWorkspaceStateTransitions> _stateTransitions;
    private readonly Mock<ISnapshotGuard> _snapshotGuard;
    private readonly Mock<IWorkspaceOperationResultFactory> _resultFactory;
    private readonly Mock<ITransactionCommitService> _commitService;
    private readonly Mock<IWorkspaceDiffBuilder> _diffBuilder;
    private readonly Mock<IWorkspaceResolverFactory> _resolverFactory;
    private readonly Mock<IWorkspaceResolver> _resolver;
    private readonly Mock<IWorkspaceInstanceStatusPublisher> _instanceStatusPublisher;
    private readonly TransactionService _target;

    public TransactionServiceTests()
    {
        _workspace = new AdhocWorkspace();
        _sessionStore = new Mock<IWorkspaceSessionStore>();
        _sessionAcquirer = new Mock<IWorkspaceSessionAcquirer>();
        SetupWorkspaceRequiredAcquisitions();
        _stateTransitions = new Mock<IWorkspaceStateTransitions>();
        _snapshotGuard = new Mock<ISnapshotGuard>();
        _resultFactory = new Mock<IWorkspaceOperationResultFactory>();
        _commitService = new Mock<ITransactionCommitService>();
        _diffBuilder = new Mock<IWorkspaceDiffBuilder>();
        _resolverFactory = new Mock<IWorkspaceResolverFactory>();
        _resolver = new Mock<IWorkspaceResolver>();
        _instanceStatusPublisher = new Mock<IWorkspaceInstanceStatusPublisher>();
        _target = new TransactionService(
            Options.Create(new WorkspaceCoordinatorOptions { MaxTransactionRevisions = 5 }),
            _sessionStore.Object,
            _sessionAcquirer.Object,
            _stateTransitions.Object,
            _snapshotGuard.Object,
            _resultFactory.Object,
            _commitService.Object,
            _diffBuilder.Object,
            _resolverFactory.Object,
            _instanceStatusPublisher.Object);
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_StartingTransaction_THEN_ShouldPropagateCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var action = async () => await _target.StartAsync(null, null, null, cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_MovingHistory_THEN_ShouldPropagateCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var action = async () => await _target.MoveHistoryAsync(
            null,
            null,
            null,
            TransactionHistoryDirection.Undo,
            null,
            cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_CommittingTransaction_THEN_ShouldPropagateCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var action = async () => await _target.CommitAsync(null, null, null, null, cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_RollingBackTransaction_THEN_ShouldPropagateCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var action = async () => await _target.RollbackAsync(null, null, null, cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_NoWorkspace_WHEN_StartingTransaction_THEN_ShouldReturnWorkspaceNotOpen()
    {
        var expected = CreateResult<TransactionStartOutcome>();
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(new WorkspaceHostSnapshot());
        SetupRejectedResult(expected, WorkspaceErrorCodes.WorkspaceNotOpen);

        var result = await _target.StartAsync(null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_SelectorFailure_WHEN_StartingTransaction_THEN_ShouldReturnSelectionError()
    {
        var session = CreateSession(transaction: null);
        var error = new WorkspaceOperationError { Code = "Code", Message = "Message" };
        var expected = CreateResult<TransactionStartOutcome>();
        var snapshot = CreateHostSnapshot(session);
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(snapshot);
        SetupRejectedResult(expected, error);
        SetupAcquisitionFailure(error);

        var result = await _target.StartAsync("WorkspaceId", null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Theory]
    [InlineData(null, "Alias", null)]
    [InlineData(null, null, "Path")]
    public async Task GIVEN_SelectorFields_WHEN_StartingTransaction_THEN_ShouldPassPopulatedSelector(
        string? workspaceId,
        string? alias,
        string? path)
    {
        var session = CreateSession(transaction: null);
        var error = new WorkspaceOperationError { Code = "Code", Message = "Message" };
        var expected = CreateResult<TransactionStartOutcome>();
        var snapshot = CreateHostSnapshot(session);
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(snapshot);
        SetupRejectedResult(expected, error);
        _sessionAcquirer.Setup(item => item.AcquireExclusive(
            It.Is<WorkspaceSelector>(selector =>
                selector.WorkspaceId == workspaceId && selector.Alias == alias && selector.Path == path)))
            .Returns(WorkspaceSessionAcquisition.Rejected(error));

        var result = await _target.StartAsync(workspaceId, alias, path, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_BusyWorkspace_WHEN_StartingTransaction_THEN_ShouldReturnWorkspaceBusy()
    {
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession(transaction: null) with { OperationGate = gate.Object };
        var expected = CreateResult<TransactionStartOutcome>();
        SetupSelection(session);
        gate.Setup(item => item.TryAcquireExclusive()).Returns((IWorkspaceOperationLease?)null);
        SetupRejectedResult(expected, WorkspaceErrorCodes.WorkspaceBusy);

        var result = await _target.StartAsync(null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_SelectedSessionDisappears_WHEN_StartingTransaction_THEN_ShouldReturnWorkspaceNotOpen()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession(transaction: null) with { OperationGate = gate.Object };
        var expected = CreateResult<TransactionStartOutcome>();
        SetupSelection(session);
        gate.Setup(item => item.TryAcquireExclusive()).Returns(operationLease.Object);
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns((WorkspaceSessionSnapshot?)null);
        _sessionAcquirer.Setup(item => item.AcquireExclusive(It.IsAny<WorkspaceSelector?>()))
            .Returns(WorkspaceSessionAcquisition.Rejected(CreateError(WorkspaceErrorCodes.WorkspaceNotOpen), lease: operationLease.Object));
        SetupRejectedResult(expected, WorkspaceErrorCodes.WorkspaceNotOpen);

        var result = await _target.StartAsync(null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_OutOfDateWorkspace_WHEN_StartingTransaction_THEN_ShouldReturnConflict()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession(transaction: null) with
        {
            OperationGate = gate.Object,
            State = WorkspaceLifecycleState.WorkspaceOutOfDate,
        };
        var expected = CreateResult<TransactionStartOutcome>();
        SetupSelection(session);
        gate.Setup(item => item.TryAcquireExclusive()).Returns(operationLease.Object);
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
        SetupConflictResult(expected, WorkspaceErrorCodes.WorkspaceOutOfDate);

        var result = await _target.StartAsync(null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Theory]
    [InlineData("OwnerAlias", "OwnerPath", "OwnerId", "OwnerAlias")]
    [InlineData(null, "OwnerPath", "OwnerId", "OwnerPath")]
    [InlineData(null, null, "OwnerId", "OwnerId")]
    public async Task GIVEN_DifferentTransactionOwner_WHEN_StartingTransaction_THEN_ShouldIdentifyOwner(
        string? ownerAlias,
        string? ownerPath,
        string ownerId,
        string expectedDisplayName)
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession(transaction: null) with { OperationGate = gate.Object };
        var ownerSession = CreateSession(CreateTransaction()) with
        {
            Workspace = new WorkspaceIdentity
            {
                WorkspaceId = ownerId,
                Alias = ownerAlias,
                LoadedPath = ownerPath!,
            },
        };
        var expected = CreateResult<TransactionStartOutcome>();
        SetupSelection(session);
        gate.Setup(item => item.TryAcquireExclusive()).Returns(operationLease.Object);
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
        _sessionStore.Setup(item => item.ReadSnapshot())
            .Returns(CreateHostSnapshot(session) with { TransactionOwnerWorkspaceId = "OwnerId" });
        _sessionStore.Setup(item => item.ReadSession("OwnerId")).Returns(ownerSession);
        _resultFactory.Setup(item => item.Rejected<TransactionStartOutcome>(
            WorkspaceErrorCodes.TransactionOwner,
            It.Is<string>(message => message.Contains(expectedDisplayName, StringComparison.Ordinal)),
            RequiredAction.CommitOrRollback,
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var result = await _target.StartAsync(null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_MissingOwnerSession_WHEN_StartingTransaction_THEN_ShouldIdentifyUnknownOwner()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession(transaction: null) with { OperationGate = gate.Object };
        var expected = CreateResult<TransactionStartOutcome>();
        SetupSelection(session);
        gate.Setup(item => item.TryAcquireExclusive()).Returns(operationLease.Object);
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
        _sessionStore.Setup(item => item.ReadSnapshot())
            .Returns(CreateHostSnapshot(session) with { TransactionOwnerWorkspaceId = "OwnerId" });
        _sessionStore.Setup(item => item.ReadSession("OwnerId")).Returns((WorkspaceSessionSnapshot?)null);
        _resultFactory.Setup(item => item.Rejected<TransactionStartOutcome>(
            WorkspaceErrorCodes.TransactionOwner,
            It.Is<string>(message => message.Contains("unknown", StringComparison.Ordinal)),
            RequiredAction.CommitOrRollback,
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var result = await _target.StartAsync(null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_ActiveTransaction_WHEN_StartingTransaction_THEN_ShouldRejectDuplicateTransaction()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession(CreateTransaction()) with { OperationGate = gate.Object };
        var expected = CreateResult<TransactionStartOutcome>();
        SetupSelection(session);
        gate.Setup(item => item.TryAcquireExclusive()).Returns(operationLease.Object);
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
        SetupRejectedResult(expected, WorkspaceErrorCodes.TransactionAlreadyActive);

        var result = await _target.StartAsync(null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_ReadyWorkspace_WHEN_StartingTransaction_THEN_ShouldStoreNewTransaction()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession(transaction: null) with { OperationGate = gate.Object };
        var expected = CreateResult<TransactionStartOutcome>();
        SetupSelection(session);
        gate.Setup(item => item.TryAcquireExclusive()).Returns(operationLease.Object);
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
        _stateTransitions.Setup(item => item.Fire(WorkspaceLifecycleState.Ready, WorkspaceTrigger.TransactionStarted))
            .Returns(WorkspaceLifecycleState.TransactionActive);
        _resultFactory.Setup(item => item.Succeeded(
            It.Is<TransactionStartOutcome>(outcome => outcome.Transaction.Revision == 0),
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var result = await _target.StartAsync(null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _sessionStore.Verify(item => item.ReplaceSessionAndSetTransactionOwner(
            It.Is<WorkspaceSessionSnapshot>(updated => updated.Transaction != null && updated.Transaction.MaxRevisions == 5),
            "WorkspaceId"), Times.Once);
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_PreviewingTransaction_THEN_ShouldPropagateCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var action = async () => await _target.PreviewAsync(
            null,
            null,
            null,
            null,
            false,
            3,
            cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_NoWorkspace_WHEN_PreviewingTransaction_THEN_ShouldReturnWorkspaceNotOpen()
    {
        var expected = CreateResult<TransactionPreviewOutcome>();
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(new WorkspaceHostSnapshot());
        SetupRejectedResult(expected, WorkspaceErrorCodes.WorkspaceNotOpen);

        var result = await _target.PreviewAsync(
            null,
            null,
            null,
            null,
            false,
            3,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_SelectorFailure_WHEN_PreviewingTransaction_THEN_ShouldReturnSelectionError()
    {
        var session = CreateSession(transaction: null);
        var error = new WorkspaceOperationError { Code = "Code", Message = "Message" };
        var expected = CreateResult<TransactionPreviewOutcome>();
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(CreateHostSnapshot(session));
        SetupRejectedResult(expected, error);
        SetupAcquisitionFailure(error);

        var result = await _target.PreviewAsync(
            "WorkspaceId",
            null,
            null,
            null,
            false,
            3,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_BusyWorkspace_WHEN_PreviewingTransaction_THEN_ShouldReturnWorkspaceBusy()
    {
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession(CreateTransaction()) with { OperationGate = gate.Object };
        var expected = CreateResult<TransactionPreviewOutcome>();
        SetupSelection(session);
        gate.Setup(item => item.TryAcquireShared()).Returns((IWorkspaceOperationLease?)null);
        SetupRejectedResult(expected, WorkspaceErrorCodes.WorkspaceBusy);

        var result = await _target.PreviewAsync(
            null,
            null,
            null,
            null,
            false,
            3,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_MissingTransaction_WHEN_PreviewingTransaction_THEN_ShouldRequireTransaction()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession(transaction: null) with { OperationGate = gate.Object };
        var expected = CreateResult<TransactionPreviewOutcome>();
        SetupSelection(session);
        gate.Setup(item => item.TryAcquireShared()).Returns(operationLease.Object);
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
        SetupRejectedResult(expected, WorkspaceErrorCodes.TransactionRequired);

        var result = await _target.PreviewAsync(
            null,
            null,
            null,
            null,
            false,
            3,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        operationLease.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_SelectedSessionDisappears_WHEN_PreviewingTransaction_THEN_ShouldRequireTransactionWithoutContext()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession(transaction: null) with { OperationGate = gate.Object };
        var expected = CreateResult<TransactionPreviewOutcome>();
        SetupSelection(session);
        gate.Setup(item => item.TryAcquireShared()).Returns(operationLease.Object);
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns((WorkspaceSessionSnapshot?)null);
        _sessionAcquirer.Setup(item => item.AcquireShared(It.IsAny<WorkspaceSelector?>()))
            .Returns(WorkspaceSessionAcquisition.Rejected(CreateError(WorkspaceErrorCodes.WorkspaceNotOpen), lease: operationLease.Object));
        SetupRejectedResult(expected, WorkspaceErrorCodes.WorkspaceNotOpen);

        var result = await _target.PreviewAsync(
            null,
            null,
            null,
            null,
            false,
            3,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_TransactionAndDiffNotRequested_WHEN_PreviewingTransaction_THEN_ShouldReturnSummaryWithoutResolvingDocument()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var transaction = CreateTransaction();
        var session = CreateSession(transaction) with { OperationGate = gate.Object };
        var changes = new ChangeSummary
        {
            Added = [new DocumentChange()],
            Modified = [new DocumentChange()],
            Deleted = [new DocumentChange()],
        };
        var expected = CreateResult<TransactionPreviewOutcome>();
        SetupSelection(session);
        gate.Setup(item => item.TryAcquireShared()).Returns(operationLease.Object);
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
        _resolverFactory.Setup(item => item.Create(transaction.CurrentSolution, session.Workspace, transaction.CurrentRevision))
            .Returns(_resolver.Object);
        _diffBuilder.Setup(item => item.CreateChangeSummaryAsync(
            transaction.BaselineSolution,
            transaction.CurrentSolution,
            _resolver.Object,
            TestContext.Current.CancellationToken)).ReturnsAsync(changes);
        _resultFactory.Setup(item => item.Succeeded(
            It.Is<TransactionPreviewOutcome>(outcome => outcome.Documents.Count == 3 && outcome.Diff == null),
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var result = await _target.PreviewAsync(
            null,
            null,
            null,
            new DocumentSelector { DocumentId = "DocumentId" },
            false,
            3,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _resolver.Verify(item => item.ResolveDocument(It.IsAny<DocumentSelector>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_UnresolvedDocument_WHEN_PreviewingWithDiff_THEN_ShouldReturnSummaryWithoutDiff()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var transaction = CreateTransaction();
        var session = CreateSession(transaction) with { OperationGate = gate.Object };
        var selector = new DocumentSelector { DocumentId = "DocumentId" };
        var expected = CreateResult<TransactionPreviewOutcome>();
        SetupPreview(session, transaction, gate, operationLease);
        _resolver.Setup(item => item.ResolveDocument(selector)).Returns(SelectorResolveResult<Document>.NotFound());
        SetupPreviewSuccess(expected, diff: null);

        var result = await _target.PreviewAsync(
            null,
            null,
            null,
            selector,
            true,
            3,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _diffBuilder.Verify(item => item.CreateDocumentDiffAsync(
            It.IsAny<Solution>(),
            It.IsAny<Solution>(),
            It.IsAny<DocumentReference>(),
            It.IsAny<IWorkspaceResolver>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ResolvedDocumentWithoutReference_WHEN_PreviewingWithDiff_THEN_ShouldReturnSummaryWithoutDiff()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var transaction = CreateTransaction();
        var session = CreateSession(transaction) with { OperationGate = gate.Object };
        var document = transaction.CurrentSolution.Projects.Single().Documents.Single();
        var selector = new DocumentSelector { DocumentId = "DocumentId" };
        var expected = CreateResult<TransactionPreviewOutcome>();
        SetupPreview(session, transaction, gate, operationLease);
        _resolver.Setup(item => item.ResolveDocument(selector)).Returns(SelectorResolveResult<Document>.Resolved(document));
        _resolver.Setup(item => item.CreateDocumentReference(document)).Returns((DocumentReference?)null);
        SetupPreviewSuccess(expected, diff: null);

        var result = await _target.PreviewAsync(
            null,
            null,
            null,
            selector,
            true,
            3,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_ResolvedDocument_WHEN_PreviewingWithDiff_THEN_ShouldReturnDetailedDiff()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var transaction = CreateTransaction();
        var session = CreateSession(transaction) with
        {
            OperationGate = gate.Object,
            State = WorkspaceLifecycleState.TransactionConflicted,
        };
        var document = transaction.CurrentSolution.Projects.Single().Documents.Single();
        var selector = new DocumentSelector { DocumentId = "DocumentId" };
        var reference = new DocumentReference { DocumentId = "DocumentId" };
        var diff = new DocumentDiff { Document = reference };
        var expected = CreateResult<TransactionPreviewOutcome>();
        SetupPreview(session, transaction, gate, operationLease);
        _resolver.Setup(item => item.ResolveDocument(selector)).Returns(SelectorResolveResult<Document>.Resolved(document));
        _resolver.Setup(item => item.CreateDocumentReference(document)).Returns(reference);
        _diffBuilder.Setup(item => item.CreateDocumentDiffAsync(
            transaction.BaselineSolution,
            transaction.CurrentSolution,
            reference,
            _resolver.Object,
            7,
            TestContext.Current.CancellationToken)).ReturnsAsync(diff);
        SetupPreviewSuccess(expected, diff);

        var result = await _target.PreviewAsync(
            null,
            null,
            null,
            selector,
            true,
            7,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_NoWorkspace_WHEN_MovingHistory_THEN_ShouldReturnWorkspaceNotOpen()
    {
        var expected = CreateResult<TransactionHistoryOutcome>();
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(new WorkspaceHostSnapshot());
        SetupRejectedResult(expected, WorkspaceErrorCodes.WorkspaceNotOpen);

        var result = await _target.MoveHistoryAsync(
            null,
            null,
            null,
            TransactionHistoryDirection.Undo,
            null,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_SelectorFailure_WHEN_MovingHistory_THEN_ShouldReturnSelectionError()
    {
        var session = CreateSession(transaction: null);
        var error = new WorkspaceOperationError { Code = "Code", Message = "Message" };
        var expected = CreateResult<TransactionHistoryOutcome>();
        var snapshot = CreateHostSnapshot(session);
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(snapshot);
        SetupRejectedResult(expected, error);
        SetupAcquisitionFailure(error);

        var result = await _target.MoveHistoryAsync(
            null,
            null,
            null,
            TransactionHistoryDirection.Undo,
            null,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_BusyWorkspace_WHEN_MovingHistory_THEN_ShouldReturnWorkspaceBusy()
    {
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession(CreateTransaction()) with { OperationGate = gate.Object };
        var expected = CreateResult<TransactionHistoryOutcome>();
        SetupSelection(session);
        gate.Setup(item => item.TryAcquireExclusive()).Returns((IWorkspaceOperationLease?)null);
        SetupRejectedResult(expected, WorkspaceErrorCodes.WorkspaceBusy);

        var result = await _target.MoveHistoryAsync(
            null,
            null,
            null,
            TransactionHistoryDirection.Undo,
            null,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_MissingTransaction_WHEN_MovingHistory_THEN_ShouldRequireTransaction()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession(transaction: null) with { OperationGate = gate.Object };
        var expected = CreateResult<TransactionHistoryOutcome>();
        SetupSelection(session);
        gate.Setup(item => item.TryAcquireExclusive()).Returns(operationLease.Object);
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
        SetupRejectedResult(expected, WorkspaceErrorCodes.TransactionRequired);

        var result = await _target.MoveHistoryAsync(
            null,
            null,
            null,
            TransactionHistoryDirection.Undo,
            null,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_SelectedSessionDisappears_WHEN_MovingHistory_THEN_ShouldRequireTransaction()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession(CreateTransaction()) with { OperationGate = gate.Object };
        var expected = CreateResult<TransactionHistoryOutcome>();
        SetupSelection(session);
        gate.Setup(item => item.TryAcquireExclusive()).Returns(operationLease.Object);
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns((WorkspaceSessionSnapshot?)null);
        _sessionAcquirer.Setup(item => item.AcquireExclusive(It.IsAny<WorkspaceSelector?>()))
            .Returns(WorkspaceSessionAcquisition.Rejected(CreateError(WorkspaceErrorCodes.WorkspaceNotOpen), lease: operationLease.Object));
        SetupRejectedResult(expected, WorkspaceErrorCodes.WorkspaceNotOpen);

        var result = await _target.MoveHistoryAsync(
            null,
            null,
            null,
            TransactionHistoryDirection.Undo,
            null,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_SnapshotMismatch_WHEN_MovingHistory_THEN_ShouldReturnConflict()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession(CreateTransaction()) with { OperationGate = gate.Object };
        var mismatch = new WorkspaceOperationError { Code = "SnapshotMismatch", Message = "Message" };
        var expected = CreateResult<TransactionHistoryOutcome>();
        SetupHistory(session, gate, operationLease);
        _snapshotGuard.Setup(item => item.Validate(session, It.IsAny<SnapshotPrecondition?>())).Returns(mismatch);
        _resultFactory.Setup(item => item.Conflict<TransactionHistoryOutcome>(
            mismatch,
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var result = await _target.MoveHistoryAsync(
            null,
            null,
            null,
            TransactionHistoryDirection.Undo,
            new SnapshotPrecondition(),
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_ConflictedTransaction_WHEN_MovingHistory_THEN_ShouldRequireRollback()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession(CreateTransaction()) with
        {
            OperationGate = gate.Object,
            State = WorkspaceLifecycleState.TransactionConflicted,
        };
        var expected = CreateResult<TransactionHistoryOutcome>();
        SetupHistory(session, gate, operationLease);
        SetupConflictResult(expected, WorkspaceErrorCodes.TransactionConflicted);

        var result = await _target.MoveHistoryAsync(
            null,
            null,
            null,
            TransactionHistoryDirection.Undo,
            null,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Theory]
    [InlineData(TransactionHistoryDirection.Undo, 0, 1)]
    [InlineData(TransactionHistoryDirection.Redo, 1, 1)]
    public async Task GIVEN_HistoryMoveUnavailable_WHEN_MovingHistory_THEN_ShouldReturnRejection(
        TransactionHistoryDirection direction,
        int currentRevision,
        int revisionCount)
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var transaction = CreateTransaction(currentRevision, revisionCount);
        var session = CreateSession(transaction) with { OperationGate = gate.Object };
        var expected = CreateResult<TransactionHistoryOutcome>();
        SetupHistory(session, gate, operationLease);
        SetupRejectedResult(expected, WorkspaceErrorCodes.TransactionHistoryUnavailable);

        var result = await _target.MoveHistoryAsync(
            null,
            null,
            null,
            direction,
            null,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Theory]
    [InlineData(TransactionHistoryDirection.Undo, 1, 1, 0)]
    [InlineData(TransactionHistoryDirection.Redo, 0, 1, 1)]
    public async Task GIVEN_HistoryMoveAvailable_WHEN_MovingHistory_THEN_ShouldUpdateRevision(
        TransactionHistoryDirection direction,
        int currentRevision,
        int revisionCount,
        int expectedRevision)
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var transaction = CreateTransaction(currentRevision, revisionCount);
        var session = CreateSession(transaction) with { OperationGate = gate.Object };
        var expected = CreateResult<TransactionHistoryOutcome>();
        SetupHistory(session, gate, operationLease);
        _resultFactory.Setup(item => item.Succeeded(
            It.Is<TransactionHistoryOutcome>(outcome => outcome.Transaction.Revision == expectedRevision),
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var result = await _target.MoveHistoryAsync(
            null,
            null,
            null,
            direction,
            null,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _sessionStore.Verify(item => item.ReplaceSession(It.Is<WorkspaceSessionSnapshot>(updated =>
            updated.Transaction!.CurrentRevision == expectedRevision)), Times.Once);
    }

    [Fact]
    public async Task GIVEN_NoWorkspace_WHEN_CommittingTransaction_THEN_ShouldReturnWorkspaceNotOpen()
    {
        var expected = CreateResult<TransactionCommitOutcome>();
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(new WorkspaceHostSnapshot());
        SetupRejectedResult(expected, WorkspaceErrorCodes.WorkspaceNotOpen);

        var result = await _target.CommitAsync(null, null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_SelectorFailure_WHEN_CommittingTransaction_THEN_ShouldReturnSelectionError()
    {
        var session = CreateSession(CreateTransaction());
        var error = new WorkspaceOperationError { Code = "Code", Message = "Message" };
        var expected = CreateResult<TransactionCommitOutcome>();
        var snapshot = CreateHostSnapshot(session);
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(snapshot);
        SetupRejectedResult(expected, error);
        SetupAcquisitionFailure(error);

        var result = await _target.CommitAsync(null, null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_BusyWorkspace_WHEN_CommittingTransaction_THEN_ShouldReturnWorkspaceBusy()
    {
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession(CreateTransaction()) with { OperationGate = gate.Object };
        var expected = CreateResult<TransactionCommitOutcome>();
        SetupSelection(session);
        gate.Setup(item => item.TryAcquireExclusive()).Returns((IWorkspaceOperationLease?)null);
        SetupRejectedResult(expected, WorkspaceErrorCodes.WorkspaceBusy);

        var result = await _target.CommitAsync(null, null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_AvailableWorkspace_WHEN_CommittingTransaction_THEN_ShouldDelegateAndDisposeLease()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession(CreateTransaction()) with { OperationGate = gate.Object };
        var expectedSnapshot = new SnapshotPrecondition();
        var expected = CreateResult<TransactionCommitOutcome>();
        SetupSelection(session);
        gate.Setup(item => item.TryAcquireExclusive()).Returns(operationLease.Object);
        _commitService.Setup(item => item.CommitAsync(
            It.Is<WorkspaceSelection>(selection => selection.WorkspaceId == "WorkspaceId"),
            expectedSnapshot,
            TestContext.Current.CancellationToken)).ReturnsAsync(expected);

        var result = await _target.CommitAsync(
            null,
            null,
            null,
            expectedSnapshot,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        operationLease.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_NoWorkspace_WHEN_RollingBackTransaction_THEN_ShouldReturnWorkspaceNotOpen()
    {
        var expected = CreateResult<TransactionRollbackOutcome>();
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(new WorkspaceHostSnapshot());
        SetupRejectedResult(expected, WorkspaceErrorCodes.WorkspaceNotOpen);

        var result = await _target.RollbackAsync(null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_SelectorFailure_WHEN_RollingBackTransaction_THEN_ShouldReturnSelectionError()
    {
        var session = CreateSession(CreateTransaction());
        var error = new WorkspaceOperationError { Code = "Code", Message = "Message" };
        var expected = CreateResult<TransactionRollbackOutcome>();
        var snapshot = CreateHostSnapshot(session);
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(snapshot);
        SetupRejectedResult(expected, error);
        SetupAcquisitionFailure(error);

        var result = await _target.RollbackAsync(null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_BusyWorkspace_WHEN_RollingBackTransaction_THEN_ShouldReturnWorkspaceBusy()
    {
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession(CreateTransaction()) with { OperationGate = gate.Object };
        var expected = CreateResult<TransactionRollbackOutcome>();
        SetupSelection(session);
        gate.Setup(item => item.TryAcquireExclusive()).Returns((IWorkspaceOperationLease?)null);
        SetupRejectedResult(expected, WorkspaceErrorCodes.WorkspaceBusy);

        var result = await _target.RollbackAsync(null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_MissingTransaction_WHEN_RollingBackTransaction_THEN_ShouldRequireTransaction()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession(transaction: null) with { OperationGate = gate.Object };
        var expected = CreateResult<TransactionRollbackOutcome>();
        SetupSelection(session);
        gate.Setup(item => item.TryAcquireExclusive()).Returns(operationLease.Object);
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
        SetupRejectedResult(expected, WorkspaceErrorCodes.TransactionRequired);

        var result = await _target.RollbackAsync(null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_SelectedSessionDisappears_WHEN_RollingBackTransaction_THEN_ShouldRequireTransaction()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession(CreateTransaction()) with { OperationGate = gate.Object };
        var expected = CreateResult<TransactionRollbackOutcome>();
        SetupSelection(session);
        gate.Setup(item => item.TryAcquireExclusive()).Returns(operationLease.Object);
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns((WorkspaceSessionSnapshot?)null);
        _sessionAcquirer.Setup(item => item.AcquireExclusive(It.IsAny<WorkspaceSelector?>()))
            .Returns(WorkspaceSessionAcquisition.Rejected(CreateError(WorkspaceErrorCodes.WorkspaceNotOpen), lease: operationLease.Object));
        SetupRejectedResult(expected, WorkspaceErrorCodes.WorkspaceNotOpen);

        var result = await _target.RollbackAsync(null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Theory]
    [InlineData(WorkspaceLifecycleState.TransactionActive, "TransactionRolledBack", TransactionRollbackState.Ready)]
    [InlineData(WorkspaceLifecycleState.TransactionConflicted, "ConflictedRollbackCompleted", TransactionRollbackState.WorkspaceOutOfDate)]
    public async Task GIVEN_Transaction_WHEN_RollingBack_THEN_ShouldRestoreBaselineAndClearOwner(
        WorkspaceLifecycleState state,
        string triggerName,
        TransactionRollbackState expectedState)
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var transaction = CreateTransaction();
        var session = CreateSession(transaction) with { OperationGate = gate.Object, State = state };
        var trigger = triggerName == "TransactionRolledBack"
            ? WorkspaceTrigger.TransactionRolledBack
            : WorkspaceTrigger.ConflictedRollbackCompleted;
        var expected = CreateResult<TransactionRollbackOutcome>();
        SetupSelection(session);
        gate.Setup(item => item.TryAcquireExclusive()).Returns(operationLease.Object);
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
        _stateTransitions.Setup(item => item.Fire(state, trigger)).Returns(WorkspaceLifecycleState.Ready);
        _resultFactory.Setup(item => item.Succeeded(
            It.Is<TransactionRollbackOutcome>(outcome => outcome.State == expectedState),
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var result = await _target.RollbackAsync(null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _sessionStore.Verify(item => item.ReplaceSessionAndSetTransactionOwner(
            It.Is<WorkspaceSessionSnapshot>(updated =>
                updated.Transaction == null && updated.CurrentSolution == transaction.BaselineSolution),
            null), Times.Once);
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    private void SetupSelection(WorkspaceSessionSnapshot session)
    {
        var hostSnapshot = CreateHostSnapshot(session);
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(hostSnapshot);
        _sessionAcquirer.Setup(item => item.AcquireShared(It.IsAny<WorkspaceSelector?>()))
            .Returns(() => CreateAcquisition(session, exclusive: false));
        _sessionAcquirer.Setup(item => item.AcquireExclusive(It.IsAny<WorkspaceSelector?>()))
            .Returns(() => CreateAcquisition(session, exclusive: true));
    }

    private WorkspaceSessionAcquisition CreateAcquisition(WorkspaceSessionSnapshot session, bool exclusive)
    {
        var lease = exclusive
            ? session.OperationGate.TryAcquireExclusive()
            : session.OperationGate.TryAcquireShared();
        return lease is null
            ? WorkspaceSessionAcquisition.Rejected(CreateError(WorkspaceErrorCodes.WorkspaceBusy), session)
            : WorkspaceSessionAcquisition.Acquired(new WorkspaceSelection
            {
                WorkspaceId = session.Workspace.WorkspaceId,
                Session = session,
            }, session, lease);
    }

    private void SetupWorkspaceRequiredAcquisitions()
    {
        var error = CreateError(WorkspaceErrorCodes.WorkspaceNotOpen);
        _sessionAcquirer.Setup(item => item.AcquireShared(It.IsAny<WorkspaceSelector?>()))
            .Returns(WorkspaceSessionAcquisition.Rejected(error));
        _sessionAcquirer.Setup(item => item.AcquireExclusive(It.IsAny<WorkspaceSelector?>()))
            .Returns(WorkspaceSessionAcquisition.Rejected(error));
    }

    private void SetupAcquisitionFailure(WorkspaceOperationError error)
    {
        _sessionAcquirer.Setup(item => item.AcquireShared(It.IsAny<WorkspaceSelector?>()))
            .Returns(WorkspaceSessionAcquisition.Rejected(error));
        _sessionAcquirer.Setup(item => item.AcquireExclusive(It.IsAny<WorkspaceSelector?>()))
            .Returns(WorkspaceSessionAcquisition.Rejected(error));
    }

    private static WorkspaceOperationError CreateError(string code)
    {
        return new WorkspaceOperationError
        {
            Code = code,
            Message = "Message",
            RequiredAction = code == WorkspaceErrorCodes.WorkspaceBusy
                ? RequiredAction.Retry
                : RequiredAction.OpenWorkspace,
        };
    }

    private void SetupPreview(
        WorkspaceSessionSnapshot session,
        WorkspaceTransaction transaction,
        Mock<IWorkspaceOperationGate> gate,
        Mock<IWorkspaceOperationLease> operationLease)
    {
        SetupSelection(session);
        gate.Setup(item => item.TryAcquireShared()).Returns(operationLease.Object);
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
        _resolverFactory.Setup(item => item.Create(transaction.CurrentSolution, session.Workspace, transaction.CurrentRevision))
            .Returns(_resolver.Object);
        _diffBuilder.Setup(item => item.CreateChangeSummaryAsync(
            transaction.BaselineSolution,
            transaction.CurrentSolution,
            _resolver.Object,
            TestContext.Current.CancellationToken)).ReturnsAsync(new ChangeSummary());
    }

    private void SetupPreviewSuccess(
        WorkspaceOperationResult<TransactionPreviewOutcome> result,
        DocumentDiff? diff)
    {
        _resultFactory.Setup(item => item.Succeeded(
            It.Is<TransactionPreviewOutcome>(outcome => outcome.Diff == diff),
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(result);
    }

    private void SetupHistory(
        WorkspaceSessionSnapshot session,
        Mock<IWorkspaceOperationGate> gate,
        Mock<IWorkspaceOperationLease> operationLease)
    {
        SetupSelection(session);
        gate.Setup(item => item.TryAcquireExclusive()).Returns(operationLease.Object);
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
    }

    private void SetupRejectedResult<TOutcome>(WorkspaceOperationResult<TOutcome> result, string code)
    {
        _resultFactory.Setup(item => item.Rejected<TOutcome>(
            code,
            It.IsAny<string>(),
            It.IsAny<RequiredAction?>(),
            It.IsAny<WorkspaceOperationContext?>(),
            null,
            null)).Returns(result);
        _resultFactory.Setup(item => item.Rejected<TOutcome>(
            It.Is<WorkspaceOperationError>(error => error.Code == code),
            It.IsAny<WorkspaceOperationContext?>(),
            null,
            null)).Returns(result);
    }

    private void SetupRejectedResult<TOutcome>(WorkspaceOperationResult<TOutcome> result, WorkspaceOperationError error)
    {
        _resultFactory.Setup(item => item.Rejected<TOutcome>(error, null, null, null)).Returns(result);
    }

    private void SetupConflictResult<TOutcome>(WorkspaceOperationResult<TOutcome> result, string code)
    {
        _resultFactory.Setup(item => item.Conflict<TOutcome>(
            code,
            It.IsAny<string>(),
            It.IsAny<RequiredAction?>(),
            It.IsAny<WorkspaceOperationContext?>(),
            null,
            null)).Returns(result);
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
            LoadedWorkspace = null!,
            CurrentSolution = transaction?.CurrentSolution ?? _workspace.CurrentSolution,
            Transaction = transaction,
            InputManifest = null!,
            OperationGate = new Mock<IWorkspaceOperationGate>().Object,
        };
    }

    private WorkspaceTransaction CreateTransaction()
    {
        var project = _workspace.AddProject("Project", LanguageNames.CSharp);
        var document = _workspace.AddDocument(project.Id, "Document.cs", SourceText.From("class C { }"));
        return new WorkspaceTransaction
        {
            BaselineSolution = document.Project.Solution,
            CurrentRevision = 0,
            MaxRevisions = 5,
        };
    }

    private WorkspaceTransaction CreateTransaction(int currentRevision, int revisionCount)
    {
        var transaction = CreateTransaction();
        var revisions = Enumerable.Range(1, revisionCount)
            .Select(_ => new WorkspaceTransactionRevision { Solution = transaction.BaselineSolution })
            .ToArray();
        return transaction with
        {
            Revisions = revisions,
            CurrentRevision = currentRevision,
        };
    }

    private static WorkspaceHostSnapshot CreateHostSnapshot(WorkspaceSessionSnapshot session)
    {
        return new WorkspaceHostSnapshot
        {
            Workspaces = new Dictionary<string, WorkspaceSessionSnapshot>
            {
                ["WorkspaceId"] = session,
            },
        };
    }

    private static WorkspaceOperationResult<TOutcome> CreateResult<TOutcome>()
    {
        return new WorkspaceOperationResult<TOutcome>();
    }
}
