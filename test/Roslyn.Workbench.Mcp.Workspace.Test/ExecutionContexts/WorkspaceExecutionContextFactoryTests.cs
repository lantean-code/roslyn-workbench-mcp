using Microsoft.Extensions.Options;

using Roslyn.Workbench.Mcp.Workspace.ChangeDetection;
using Roslyn.Workbench.Mcp.Workspace.Configuration;
using Roslyn.Workbench.Mcp.Workspace.Coordination;
using Roslyn.Workbench.Mcp.Workspace.Loading;
using Roslyn.Workbench.Mcp.Workspace.Selection;

namespace Roslyn.Workbench.Mcp.Workspace.Test.ExecutionContexts;

public sealed class WorkspaceExecutionContextFactoryTests : IDisposable
{
    private readonly AdhocWorkspace _workspace;
    private readonly Mock<IWorkspaceSessionStore> _sessionStore;
    private readonly Mock<IWorkspaceSessionAcquirer> _sessionAcquirer;
    private readonly Mock<IWorkspaceChangeDetector> _changeDetector;
    private readonly Mock<IWorkspaceStateTransitions> _stateTransitions;
    private readonly Mock<IMutationStagingService> _stagingService;
    private readonly Mock<IWorkspaceResolverFactory> _resolverFactory;
    private readonly Mock<IWorkspaceResolver> _resolver;
    private readonly Mock<ILoadedWorkspace> _loadedWorkspace;
    private readonly Mock<IWorkspaceInstanceStatusPublisher> _instanceStatusPublisher;
    private readonly WorkspaceExecutionContextFactory _target;

    public WorkspaceExecutionContextFactoryTests()
    {
        _workspace = new AdhocWorkspace();
        _sessionStore = new Mock<IWorkspaceSessionStore>();
        _sessionAcquirer = new Mock<IWorkspaceSessionAcquirer>();
        SetupWorkspaceRequiredAcquisitions();
        _changeDetector = new Mock<IWorkspaceChangeDetector>();
        _stateTransitions = new Mock<IWorkspaceStateTransitions>();
        _stagingService = new Mock<IMutationStagingService>();
        _resolverFactory = new Mock<IWorkspaceResolverFactory>();
        _resolver = new Mock<IWorkspaceResolver>();
        _loadedWorkspace = new Mock<ILoadedWorkspace>();
        _instanceStatusPublisher = new Mock<IWorkspaceInstanceStatusPublisher>();
        _resolverFactory.Setup(item => item.Create(It.IsAny<Solution>(), It.IsAny<WorkspaceIdentity>(), It.IsAny<int?>()))
            .Returns(_resolver.Object);

        _target = new WorkspaceExecutionContextFactory(
            Options.Create(new WorkspaceOptions { DefaultMaxResults = 25 }),
            _sessionStore.Object,
            _sessionAcquirer.Object,
            _changeDetector.Object,
            _stateTransitions.Object,
            _stagingService.Object,
            _resolverFactory.Object,
            _instanceStatusPublisher.Object);
    }

    [Fact]
    public void GIVEN_CancelledToken_WHEN_CreatingQueryContext_THEN_ShouldPropagateCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var action = () => _target.CreateQueryContext(workspace: null, cancellationSource.Token);

        action.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void GIVEN_CancelledToken_WHEN_CreatingMutationContext_THEN_ShouldPropagateCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var action = () => _target.CreateMutationContext(workspace: null, cancellationSource.Token);

        action.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void GIVEN_CancellationDuringValidation_WHEN_CreatingQueryContext_THEN_ShouldReleaseAcquiredLease()
    {
        using var cancellationSource = new CancellationTokenSource();
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        gate.Setup(item => item.TryAcquireShared()).Returns(operationLease.Object);
        var session = CreateSession(gate.Object);
        SetupSelection(session);
        _changeDetector
            .Setup(item => item.HasChanged(session.InputManifest, cancellationSource.Token))
            .Throws(new OperationCanceledException(cancellationSource.Token));

        var action = () => _target.CreateQueryContext(workspace: null, cancellationSource.Token);

        action.Should().Throw<OperationCanceledException>();
        operationLease.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public void GIVEN_CancellationDuringValidation_WHEN_CreatingMutationContext_THEN_ShouldReleaseAcquiredLease()
    {
        using var cancellationSource = new CancellationTokenSource();
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        gate.Setup(item => item.TryAcquireExclusive()).Returns(operationLease.Object);
        var session = CreateSession(gate.Object);
        SetupSelection(session);
        _changeDetector
            .Setup(item => item.HasChanged(session.InputManifest, cancellationSource.Token))
            .Throws(new OperationCanceledException(cancellationSource.Token));

        var action = () => _target.CreateMutationContext(workspace: null, cancellationSource.Token);

        action.Should().Throw<OperationCanceledException>();
        operationLease.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public void GIVEN_NoLoadedWorkspaces_WHEN_CreatingQueryContext_THEN_ShouldReturnWorkspaceRequiredFailure()
    {
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(new WorkspaceHostSnapshot());

        var result = _target.CreateQueryContext(workspace: null, CancellationToken.None);

        result.Context.Should().BeNull();
        result.Failure!.Error.Code.Should().Be(WorkspaceErrorCodes.WorkspaceNotOpen);
        result.Failure.Error.RequiredAction.Should().Be(RequiredAction.OpenWorkspace);
    }

    [Fact]
    public void GIVEN_NoLoadedWorkspaces_WHEN_CreatingMutationContext_THEN_ShouldReturnWorkspaceRequiredFailure()
    {
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(new WorkspaceHostSnapshot());

        var result = _target.CreateMutationContext(workspace: null, CancellationToken.None);

        result.Context.Should().BeNull();
        result.Stager.Should().BeNull();
        result.Failure!.Error.Code.Should().Be(WorkspaceErrorCodes.WorkspaceNotOpen);
    }

    [Fact]
    public void GIVEN_SelectionFailure_WHEN_CreatingQueryContext_THEN_ShouldPreserveSelectionError()
    {
        var session = CreateSession(new Mock<IWorkspaceOperationGate>().Object);
        var snapshot = CreateHostSnapshot(session);
        var error = new WorkspaceOperationError { Code = "Code", Message = "Message" };
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(snapshot);
        _sessionAcquirer.Setup(item => item.AcquireShared(null)).Returns(WorkspaceSessionAcquisition.Rejected(error));

        var result = _target.CreateQueryContext(workspace: null, CancellationToken.None);

        result.Failure!.Status.Should().Be(WorkspaceOperationStatus.Rejected);
        result.Failure.Error.Should().BeSameAs(error);
    }

    [Fact]
    public void GIVEN_SelectionFailure_WHEN_CreatingMutationContext_THEN_ShouldPreserveSelectionError()
    {
        var session = CreateSession(new Mock<IWorkspaceOperationGate>().Object);
        var snapshot = CreateHostSnapshot(session);
        var error = new WorkspaceOperationError { Code = "Code", Message = "Message" };
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(snapshot);
        _sessionAcquirer.Setup(item => item.AcquireExclusive(null)).Returns(WorkspaceSessionAcquisition.Rejected(error));

        var result = _target.CreateMutationContext(workspace: null, CancellationToken.None);

        result.Failure!.Error.Should().BeSameAs(error);
    }

    [Fact]
    public void GIVEN_SharedGateRejection_WHEN_CreatingQueryContext_THEN_ShouldReturnBusyFailure()
    {
        var gate = new Mock<IWorkspaceOperationGate>();
        gate.Setup(item => item.TryAcquireShared()).Returns((IWorkspaceOperationLease?)null);
        var session = CreateSession(gate.Object);
        SetupSelection(session);

        var result = _target.CreateQueryContext(workspace: null, CancellationToken.None);

        result.Failure!.Error.Code.Should().Be(WorkspaceErrorCodes.WorkspaceBusy);
        result.Failure.Error.RequiredAction.Should().Be(RequiredAction.Retry);
    }

    [Fact]
    public void GIVEN_ExclusiveGateRejection_WHEN_CreatingMutationContext_THEN_ShouldReturnBusyFailure()
    {
        var gate = new Mock<IWorkspaceOperationGate>();
        gate.Setup(item => item.TryAcquireExclusive()).Returns((IWorkspaceOperationLease?)null);
        var session = CreateSession(gate.Object);
        SetupSelection(session);

        var result = _target.CreateMutationContext(workspace: null, CancellationToken.None);

        result.Failure!.Error.Code.Should().Be(WorkspaceErrorCodes.WorkspaceBusy);
    }

    [Fact]
    public async Task GIVEN_SelectedSessionDisappears_WHEN_CreatingQueryContext_THEN_ShouldReturnRequiredFailureAndRetainLease()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        gate.Setup(item => item.TryAcquireShared()).Returns(operationLease.Object);
        var session = CreateSession(gate.Object);
        SetupSelection(session, sessionRemains: false);

        var result = _target.CreateQueryContext(workspace: null, CancellationToken.None);
        await result.DisposeAsync();

        result.Failure!.Error.Code.Should().Be(WorkspaceErrorCodes.WorkspaceNotOpen);
        operationLease.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_SelectedSessionDisappears_WHEN_CreatingMutationContext_THEN_ShouldReturnRequiredFailureAndRetainLease()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        gate.Setup(item => item.TryAcquireExclusive()).Returns(operationLease.Object);
        var session = CreateSession(gate.Object);
        SetupSelection(session, sessionRemains: false);

        var result = _target.CreateMutationContext(workspace: null, CancellationToken.None);
        await result.DisposeAsync();

        result.Failure!.Error.Code.Should().Be(WorkspaceErrorCodes.WorkspaceNotOpen);
        operationLease.Verify(item => item.Dispose(), Times.Once);
    }

    [Theory]
    [InlineData(WorkspaceLifecycleState.WorkspaceOutOfDate, "WorkspaceOutOfDate")]
    [InlineData(WorkspaceLifecycleState.TransactionConflicted, "TransactionConflicted")]
    public void GIVEN_UnavailableQueryState_WHEN_CreatingQueryContext_THEN_ShouldReturnConflict(
        WorkspaceLifecycleState state,
        string errorCode)
    {
        var gate = new Mock<IWorkspaceOperationGate>();
        gate.Setup(item => item.TryAcquireShared()).Returns(new Mock<IWorkspaceOperationLease>().Object);
        var session = CreateSession(gate.Object, state);
        SetupSelection(session);

        var result = _target.CreateQueryContext(workspace: null, CancellationToken.None);

        result.Context.Should().NotBeNull();
        result.Failure!.Status.Should().Be(WorkspaceOperationStatus.Conflict);
        result.Failure.Error.Code.Should().Be(errorCode);
        _changeDetector.Verify(
            item => item.HasChanged(It.IsAny<WorkspaceInputManifest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void GIVEN_ChangedReadySession_WHEN_CreatingQueryContext_THEN_ShouldTransitionAndReplaceSession()
    {
        var gate = new Mock<IWorkspaceOperationGate>();
        gate.Setup(item => item.TryAcquireShared()).Returns(new Mock<IWorkspaceOperationLease>().Object);
        var session = CreateSession(gate.Object);
        var transitioned = session with { State = WorkspaceLifecycleState.WorkspaceOutOfDate };
        SetupSelection(session);
        _changeDetector.Setup(item => item.HasChanged(session.InputManifest, CancellationToken.None)).Returns(true);
        _stateTransitions.Setup(item => item.ApplyExternalChangeDetected(session)).Returns(transitioned);

        var result = _target.CreateQueryContext(workspace: null, CancellationToken.None);

        result.Failure!.Error.Code.Should().Be(WorkspaceErrorCodes.WorkspaceOutOfDate);
        _sessionStore.Verify(item => item.ReplaceSession(transitioned), Times.Once);
        _instanceStatusPublisher.Verify(item => item.QueueUpdate(
            transitioned.Workspace.WorkspaceId,
            WorkspaceLifecycleState.WorkspaceOutOfDate,
            1,
            null,
            null), Times.Once);
    }

    [Fact]
    public void GIVEN_LiveWorkspaceChanged_WHEN_CreatingQueryContext_THEN_ShouldContainWithoutInspectingFiles()
    {
        var gate = new Mock<IWorkspaceOperationGate>();
        gate.Setup(item => item.TryAcquireShared()).Returns(new Mock<IWorkspaceOperationLease>().Object);
        var session = CreateSession(gate.Object);
        var transitioned = session with { State = WorkspaceLifecycleState.WorkspaceOutOfDate };
        SetupSelection(session);
        _loadedWorkspace.SetupGet(item => item.HasCurrentSolutionChanged).Returns(true);
        _stateTransitions.Setup(item => item.ApplyExternalChangeDetected(session)).Returns(transitioned);

        var result = _target.CreateQueryContext(workspace: null, CancellationToken.None);

        result.Failure!.Error.Code.Should().Be(WorkspaceErrorCodes.WorkspaceOutOfDate);
        _changeDetector.Verify(
            item => item.HasChanged(It.IsAny<WorkspaceInputManifest>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _sessionStore.Verify(item => item.ReplaceSession(transitioned), Times.Once);
        _instanceStatusPublisher.Verify(item => item.QueueUpdate(
            transitioned.Workspace.WorkspaceId,
            WorkspaceLifecycleState.WorkspaceOutOfDate,
            1,
            null,
            null), Times.Once);
    }

    [Fact]
    public void GIVEN_LiveWorkspaceChangedAfterQueryInvocation_WHEN_DetectingChange_THEN_ShouldContainResult()
    {
        var session = CreateSession(new Mock<IWorkspaceOperationGate>().Object);
        var transitioned = session with { State = WorkspaceLifecycleState.WorkspaceOutOfDate };
        _sessionStore.Setup(item => item.ReadSession(session.Workspace.WorkspaceId)).Returns(session);
        _loadedWorkspace.SetupGet(item => item.HasCurrentSolutionChanged).Returns(true);
        _stateTransitions.Setup(item => item.ApplyExternalChangeDetected(session)).Returns(transitioned);

        var result = _target.DetectUnexpectedWorkspaceChange(session.Workspace.WorkspaceId);

        result!.Error.Code.Should().Be(WorkspaceErrorCodes.WorkspaceOutOfDate);
        result.Error.RequiredAction.Should().Be(RequiredAction.ReloadWorkspace);
        _sessionStore.Verify(item => item.ReplaceSession(transitioned), Times.Once);
    }

    [Fact]
    public void GIVEN_LiveWorkspaceChangedDuringTransaction_WHEN_DetectingChange_THEN_ShouldConflictTransaction()
    {
        var session = CreateSession(
            new Mock<IWorkspaceOperationGate>().Object,
            WorkspaceLifecycleState.TransactionActive);

        var transitioned = session with { State = WorkspaceLifecycleState.TransactionConflicted };
        _sessionStore.Setup(item => item.ReadSession(session.Workspace.WorkspaceId)).Returns(session);
        _loadedWorkspace.SetupGet(item => item.HasCurrentSolutionChanged).Returns(true);
        _stateTransitions.Setup(item => item.ApplyExternalChangeDetected(session)).Returns(transitioned);

        var result = _target.DetectUnexpectedWorkspaceChange(session.Workspace.WorkspaceId);

        result!.Error.Code.Should().Be(WorkspaceErrorCodes.TransactionConflicted);
        result.Error.RequiredAction.Should().Be(RequiredAction.RollbackTransaction);
        _sessionStore.Verify(item => item.ReplaceSession(transitioned), Times.Once);
    }

    [Fact]
    public void GIVEN_MissingWorkspace_WHEN_DetectingChange_THEN_ShouldRequireOpenWorkspace()
    {
        _sessionStore.Setup(item => item.ReadSession(Guid.Parse("11111111-1111-1111-1111-111111111111")))
            .Returns((WorkspaceSessionSnapshot?)null);

        var result = _target.DetectUnexpectedWorkspaceChange(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        result!.Error.Code.Should().Be(WorkspaceErrorCodes.WorkspaceNotOpen);
        result.Error.RequiredAction.Should().Be(RequiredAction.OpenWorkspace);
    }

    [Fact]
    public void GIVEN_LiveWorkspaceUnchanged_WHEN_DetectingChange_THEN_ShouldReturnNull()
    {
        var session = CreateSession(new Mock<IWorkspaceOperationGate>().Object);
        _sessionStore.Setup(item => item.ReadSession(session.Workspace.WorkspaceId)).Returns(session);

        var result = _target.DetectUnexpectedWorkspaceChange(session.Workspace.WorkspaceId);

        result.Should().BeNull();
        _sessionStore.Verify(
            item => item.ReplaceSession(It.IsAny<WorkspaceSessionSnapshot>()),
            Times.Never);
    }

    [Fact]
    public void GIVEN_ReadySession_WHEN_CreatingQueryContext_THEN_ShouldReturnNarrowContext()
    {
        var gate = new Mock<IWorkspaceOperationGate>();
        gate.Setup(item => item.TryAcquireShared()).Returns(new Mock<IWorkspaceOperationLease>().Object);
        var session = CreateSession(gate.Object);
        SetupSelection(session);
        _changeDetector.Setup(item => item.HasChanged(session.InputManifest, CancellationToken.None)).Returns(false);

        var result = _target.CreateQueryContext(workspace: null, CancellationToken.None);

        result.Failure.Should().BeNull();
        result.Context!.CurrentSolution.Should().BeSameAs(_workspace.CurrentSolution);
        result.Context.WorkspaceIdentity.Should().BeSameAs(session.Workspace);
        result.Context.SnapshotIdentity.Should().Be(session.CurrentSnapshotIdentity);
        result.Context.TransactionRevision.Should().Be(1);
        result.Context.DefaultMaxResults.Should().Be(25);
        result.Context.WorkspaceResolver.Should().BeSameAs(_resolver.Object);
    }

    [Fact]
    public void GIVEN_ActiveUnchangedSession_WHEN_CreatingQueryContext_THEN_ShouldReturnContext()
    {
        var gate = new Mock<IWorkspaceOperationGate>();
        gate.Setup(item => item.TryAcquireShared()).Returns(new Mock<IWorkspaceOperationLease>().Object);
        var session = CreateSession(gate.Object, WorkspaceLifecycleState.TransactionActive);
        SetupSelection(session);
        _changeDetector.Setup(item => item.HasChanged(session.InputManifest, CancellationToken.None)).Returns(false);

        var result = _target.CreateQueryContext(workspace: null, CancellationToken.None);

        result.Failure.Should().BeNull();
        result.Context!.TransactionRevision.Should().Be(1);
    }

    [Theory]
    [InlineData(WorkspaceLifecycleState.WorkspaceOutOfDate, "WorkspaceOutOfDate")]
    [InlineData(WorkspaceLifecycleState.TransactionConflicted, "TransactionConflicted")]
    public void GIVEN_UnavailableMutationState_WHEN_CreatingMutationContext_THEN_ShouldReturnFailureWithContextAndStager(
        WorkspaceLifecycleState state,
        string errorCode)
    {
        var gate = new Mock<IWorkspaceOperationGate>();
        gate.Setup(item => item.TryAcquireExclusive()).Returns(new Mock<IWorkspaceOperationLease>().Object);
        var session = CreateSession(gate.Object, state);
        SetupSelection(session);

        var result = _target.CreateMutationContext(workspace: null, CancellationToken.None);

        result.Context.Should().NotBeNull();
        result.Stager.Should().NotBeNull();
        result.Failure!.Error.Code.Should().Be(errorCode);
        _changeDetector.Verify(
            item => item.HasChanged(It.IsAny<WorkspaceInputManifest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void GIVEN_NoTransaction_WHEN_CreatingMutationContext_THEN_ShouldRequireTransaction()
    {
        var gate = new Mock<IWorkspaceOperationGate>();
        gate.Setup(item => item.TryAcquireExclusive()).Returns(new Mock<IWorkspaceOperationLease>().Object);
        var session = CreateSession(gate.Object, hasTransaction: false);
        SetupSelection(session);

        var result = _target.CreateMutationContext(workspace: null, CancellationToken.None);

        result.Failure!.Error.Code.Should().Be(WorkspaceErrorCodes.TransactionRequired);
        result.Failure.Error.RequiredAction.Should().Be(RequiredAction.StartTransaction);
    }

    [Theory]
    [InlineData("OwnerAlias", "OwnerPath", "OwnerAlias")]
    [InlineData(null, "OwnerPath", "OwnerPath")]
    [InlineData(null, null, "77777777-7777-7777-7777-777777777777")]
    public void GIVEN_DifferentTransactionOwner_WHEN_CreatingMutationContext_THEN_ShouldIdentifyOwner(
        string? ownerAlias,
        string? ownerPath,
        string expectedDisplayName)
    {
        var gate = new Mock<IWorkspaceOperationGate>();
        gate.Setup(item => item.TryAcquireExclusive()).Returns(new Mock<IWorkspaceOperationLease>().Object);
        var session = CreateSession(gate.Object);
        var ownerSession = CreateSession(new Mock<IWorkspaceOperationGate>().Object) with
        {
            Workspace = new WorkspaceIdentity
            {
                WorkspaceId = Guid.Parse("77777777-7777-7777-7777-777777777777"),
                Alias = ownerAlias,
                LoadedPath = ownerPath!,
            },
        };

        SetupSelection(session, ownerWorkspaceId: Guid.Parse("77777777-7777-7777-7777-777777777777"), ownerSession: ownerSession);

        var result = _target.CreateMutationContext(workspace: null, CancellationToken.None);

        result.Failure!.Error.Code.Should().Be(WorkspaceErrorCodes.TransactionOwner);
        result.Failure.Error.Message.Should().Contain(expectedDisplayName);
        result.Failure.Error.RequiredAction.Should().Be(RequiredAction.CommitOrRollback);
    }

    [Fact]
    public void GIVEN_ChangedMutationSession_WHEN_CreatingMutationContext_THEN_ShouldTransitionAndReturnConflict()
    {
        var gate = new Mock<IWorkspaceOperationGate>();
        gate.Setup(item => item.TryAcquireExclusive()).Returns(new Mock<IWorkspaceOperationLease>().Object);
        var session = CreateSession(gate.Object, WorkspaceLifecycleState.TransactionActive);
        var transitionedSolution = session.CurrentSolution.AddProject("Transitioned", "Transitioned", LanguageNames.CSharp).Solution;
        var transitioned = session with
        {
            State = WorkspaceLifecycleState.TransactionConflicted,
            CurrentSolution = transitionedSolution,
        };

        SetupSelection(session);
        _changeDetector.Setup(item => item.HasChanged(session.InputManifest, CancellationToken.None)).Returns(true);
        _stateTransitions.Setup(item => item.ApplyExternalChangeDetected(session)).Returns(transitioned);

        var result = _target.CreateMutationContext(workspace: null, CancellationToken.None);

        result.Failure!.Error.Code.Should().Be(WorkspaceErrorCodes.TransactionConflicted);
        result.Context!.CurrentSolution.Should().BeSameAs(transitionedSolution);
        _sessionStore.Verify(item => item.ReplaceSession(transitioned), Times.Once);
    }

    [Fact]
    public void GIVEN_MissingTransactionOwnerSession_WHEN_CreatingMutationContext_THEN_ShouldIdentifyUnknownOwner()
    {
        var gate = new Mock<IWorkspaceOperationGate>();
        gate.Setup(item => item.TryAcquireExclusive()).Returns(new Mock<IWorkspaceOperationLease>().Object);
        var session = CreateSession(gate.Object);
        SetupSelection(session, ownerWorkspaceId: Guid.Parse("77777777-7777-7777-7777-777777777777"), ownerSession: null);

        var result = _target.CreateMutationContext(workspace: null, CancellationToken.None);

        result.Failure!.Error.Code.Should().Be(WorkspaceErrorCodes.TransactionOwner);
        result.Failure.Error.Message.Should().Contain("unknown");
    }

    [Fact]
    public void GIVEN_TransactionAtCapacity_WHEN_CreatingMutationContext_THEN_ShouldRequireHistoryReduction()
    {
        var gate = new Mock<IWorkspaceOperationGate>();
        gate.Setup(item => item.TryAcquireExclusive()).Returns(new Mock<IWorkspaceOperationLease>().Object);
        var transaction = new WorkspaceTransaction
        {
            TransactionId = new WorkspaceTransactionId(1),
            BaselineSnapshotId = new WorkspaceSnapshotId(1),
            BaselineSolution = _workspace.CurrentSolution,
            Revisions =
            [
                new WorkspaceTransactionRevision
                {
                    SnapshotId = new WorkspaceSnapshotId(2),
                    Solution = _workspace.CurrentSolution,
                    Changes = new ChangeSummary(),
                    Operation = "Operation",
                    Summary = "Summary",
                    Preview = new MutationPreview(),
                },
                new WorkspaceTransactionRevision
                {
                    SnapshotId = new WorkspaceSnapshotId(3),
                    Solution = _workspace.CurrentSolution,
                    Changes = new ChangeSummary(),
                    Operation = "Operation",
                    Summary = "Summary",
                    Preview = new MutationPreview(),
                },
            ],
            CurrentRevision = 2,
            MaxRevisions = 2,
        };

        var session = CreateSession(gate.Object, transaction: transaction);
        SetupSelection(session);

        var result = _target.CreateMutationContext(workspace: null, CancellationToken.None);

        result.Failure!.Error.Code.Should().Be(WorkspaceErrorCodes.TransactionCapacity);
        result.Failure.Error.RequiredAction.Should().Be(RequiredAction.ReduceTransactionHistory);
    }

    [Fact]
    public void GIVEN_ValidTransaction_WHEN_CreatingMutationContext_THEN_ShouldReturnSeparateContextAndStager()
    {
        var gate = new Mock<IWorkspaceOperationGate>();
        gate.Setup(item => item.TryAcquireExclusive()).Returns(new Mock<IWorkspaceOperationLease>().Object);
        var session = CreateSession(gate.Object);
        SetupSelection(session);

        var result = _target.CreateMutationContext(workspace: null, CancellationToken.None);

        result.Failure.Should().BeNull();
        result.Context.Should().NotBeNull();
        result.Stager.Should().BeOfType<WorkspaceMutationStager>();
        result.Context.Should().NotBeAssignableTo<IWorkspaceMutationStager>();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    private void SetupSelection(
        WorkspaceSessionSnapshot selectedSession,
        bool sessionRemains = true,
        Guid? ownerWorkspaceId = null,
        WorkspaceSessionSnapshot? ownerSession = null)
    {
        var snapshot = CreateHostSnapshot(selectedSession, ownerWorkspaceId);
        _sessionStore.SetupSequence(item => item.ReadSnapshot()).Returns(snapshot).Returns(snapshot);
        _sessionStore
            .Setup(item => item.ReadSession(selectedSession.Workspace.WorkspaceId))
            .Returns(sessionRemains ? selectedSession : null);

        if (ownerWorkspaceId is not null)
        {
            _sessionStore.Setup(item => item.ReadSession(ownerWorkspaceId.Value)).Returns(ownerSession);
        }

        _sessionAcquirer.Setup(item => item.AcquireShared(null)).Returns(() => CreateAcquisition(selectedSession, sessionRemains, exclusive: false));
        _sessionAcquirer.Setup(item => item.AcquireExclusive(null)).Returns(() => CreateAcquisition(selectedSession, sessionRemains, exclusive: true));
    }

    private static WorkspaceSessionAcquisition CreateAcquisition(
        WorkspaceSessionSnapshot session,
        bool sessionRemains,
        bool exclusive)
    {
        IWorkspaceOperationLease? lease;
        if (exclusive)
        {
            lease = session.OperationGate.TryAcquireExclusive();
        }
        else
        {
            lease = session.OperationGate.TryAcquireShared();
        }

        if (lease is null)
        {
            return WorkspaceSessionAcquisition.Rejected(CreateError(WorkspaceErrorCodes.WorkspaceBusy), session);
        }

        if (!sessionRemains)
        {
            return WorkspaceSessionAcquisition.Rejected(
                CreateError(WorkspaceErrorCodes.WorkspaceNotOpen),
                lease: lease);
        }

        var selection = new WorkspaceSelection
        {
            WorkspaceId = session.Workspace.WorkspaceId,
            Session = session,
        };

        return WorkspaceSessionAcquisition.Acquired(selection, session, lease);
    }

    private void SetupWorkspaceRequiredAcquisitions()
    {
        var error = CreateError(WorkspaceErrorCodes.WorkspaceNotOpen);
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

    private WorkspaceSessionSnapshot CreateSession(
        IWorkspaceOperationGate gate,
        WorkspaceLifecycleState state = WorkspaceLifecycleState.Ready,
        WorkspaceTransaction? transaction = default,
        bool hasTransaction = true)
    {
        var committedSnapshotId = new WorkspaceSnapshotId(1);
        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            WorkspaceEpoch = 1,
            Alias = "Alias",
            LoadedPath = "LoadedPath",
        };

        var effectiveTransaction = transaction;
        if (hasTransaction && effectiveTransaction is null)
        {
            effectiveTransaction = new WorkspaceTransaction
            {
                TransactionId = new WorkspaceTransactionId(1),
                BaselineSnapshotId = committedSnapshotId,
                BaselineSolution = _workspace.CurrentSolution,
                Revisions =
                [
                    new WorkspaceTransactionRevision
                    {
                        SnapshotId = new WorkspaceSnapshotId(2),
                        Solution = _workspace.CurrentSolution,
                        Changes = new ChangeSummary(),
                        Operation = "Operation",
                        Summary = "Summary",
                        Preview = new MutationPreview(),
                    },
                ],
                CurrentRevision = 1,
                MaxRevisions = 2,
            };
        }

        return new WorkspaceSessionSnapshot
        {
            CommittedSnapshotId = committedSnapshotId,
            State = state,
            Workspace = workspaceIdentity,
            LoadedWorkspace = _loadedWorkspace.Object,
            CurrentSolution = _workspace.CurrentSolution,
            Transaction = hasTransaction ? effectiveTransaction : null,
            InputManifest = new WorkspaceInputManifest(),
            OperationGate = gate,
            CurrentSnapshotIdentity = WorkspaceSnapshotIdentity.Create(
                workspaceIdentity,
                committedSnapshotId,
                hasTransaction ? effectiveTransaction : null),
        };
    }

    private static WorkspaceHostSnapshot CreateHostSnapshot(WorkspaceSessionSnapshot session, Guid? ownerWorkspaceId = null)
    {
        return new WorkspaceHostSnapshot
        {
            Workspaces = new Dictionary<Guid, WorkspaceSessionSnapshot>
            {
                [session.Workspace.WorkspaceId] = session,
            },
            TransactionOwnerWorkspaceId = ownerWorkspaceId,
        };
    }
}
