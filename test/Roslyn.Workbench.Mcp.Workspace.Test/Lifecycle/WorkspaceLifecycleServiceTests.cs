using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Workspace.ChangeDetection;
using Roslyn.Workbench.Mcp.Workspace.Configuration;
using Roslyn.Workbench.Mcp.Workspace.Coordination;
using Roslyn.Workbench.Mcp.Workspace.Lifecycle;
using Roslyn.Workbench.Mcp.Workspace.Loading;
using Roslyn.Workbench.Mcp.Workspace.Recovery;
using Roslyn.Workbench.Mcp.Workspace.Selection;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Lifecycle;

public sealed class WorkspaceLifecycleServiceTests : IDisposable
{
    private readonly AdhocWorkspace _workspace;
    private readonly Mock<IWorkspaceSessionStore> _sessionStore;
    private readonly Mock<IWorkspaceSessionAcquirer> _sessionAcquirer;
    private readonly Mock<IWorkspaceLoader> _workspaceLoader;
    private readonly Mock<IWorkspaceRootResolver> _workspaceRootResolver;
    private readonly Mock<IWorkspaceLoadWorkflow> _workspaceLoadWorkflow;
    private readonly Mock<IWorkspaceChangeDetector> _changeDetector;
    private readonly Mock<IWorkspaceStateTransitions> _stateTransitions;
    private readonly Mock<IWorkspaceOperationResultFactory> _resultFactory;
    private readonly Mock<ICommitRecoveryStore> _recoveryStore;
    private readonly Mock<IWorkspaceInstanceStatusPublisher> _instanceStatusPublisher;
    private readonly WorkspaceLifecycleService _target;

    public WorkspaceLifecycleServiceTests()
    {
        _workspace = new AdhocWorkspace();
        _sessionStore = new Mock<IWorkspaceSessionStore>();
        _sessionAcquirer = new Mock<IWorkspaceSessionAcquirer>();
        SetupWorkspaceRequiredAcquisitions();
        _workspaceLoader = new Mock<IWorkspaceLoader>();
        _workspaceRootResolver = new Mock<IWorkspaceRootResolver>();
        _workspaceRootResolver.Setup(item => item.Resolve(It.IsAny<string>(), It.IsAny<string?>())).Returns("/workspace");
        _workspaceLoadWorkflow = new Mock<IWorkspaceLoadWorkflow>();
        _changeDetector = new Mock<IWorkspaceChangeDetector>();
        _stateTransitions = new Mock<IWorkspaceStateTransitions>();
        _resultFactory = new Mock<IWorkspaceOperationResultFactory>();
        _recoveryStore = new Mock<ICommitRecoveryStore>();
        _instanceStatusPublisher = new Mock<IWorkspaceInstanceStatusPublisher>();
        _instanceStatusPublisher
            .Setup(item => item.GetOtherLiveInstancesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _target = new WorkspaceLifecycleService(
            Options.Create(new WorkspaceCoordinatorOptions
            {
                MaxConcurrentQueries = 3,
                MaxLoadedWorkspaces = 2,
                StateDirectory = "StateDirectory",
            }),
            _sessionStore.Object,
            _sessionAcquirer.Object,
            _workspaceLoader.Object,
            _workspaceRootResolver.Object,
            _workspaceLoadWorkflow.Object,
            _changeDetector.Object,
            _stateTransitions.Object,
            _resultFactory.Object,
            _recoveryStore.Object,
            _instanceStatusPublisher.Object);
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_OpeningWorkspace_THEN_ShouldPropagateCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var action = async () => await _target.OpenAsync("Path", null, cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_ListingWorkspaces_THEN_ShouldPropagateCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var action = async () => await _target.ListAsync(cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_ClosingWorkspace_THEN_ShouldPropagateCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var action = async () => await _target.CloseAsync(null, null, null, cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_GettingStatus_THEN_ShouldPropagateCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var action = async () => await _target.GetStatusAsync(
            null,
            null,
            null,
            StatusDetailLevel.Standard,
            cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_ReloadingWorkspace_THEN_ShouldPropagateCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var action = async () => await _target.ReloadAsync(null, null, null, cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_InvalidPath_WHEN_OpeningWorkspace_THEN_ShouldReturnRejection()
    {
        var expected = CreateResult<WorkspaceOpenOutcome>();
        _workspaceLoader.Setup(item => item.NormalizeOpenPath("Path")).Returns((string?)null);
        SetupRejectedResult(expected, "WorkspacePathInvalid");

        var result = await _target.OpenAsync("Path", null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_InvalidExplicitWorkspaceRoot_WHEN_OpeningWorkspace_THEN_ShouldReturnRejection()
    {
        var expected = CreateResult<WorkspaceOpenOutcome>();
        _workspaceLoader.Setup(item => item.NormalizeOpenPath("Path")).Returns("/workspace/Project.csproj");
        _workspaceRootResolver.Setup(item => item.Resolve("/workspace/Project.csproj", "/other")).Returns((string?)null);
        SetupRejectedResult(expected, "WorkspaceRootInvalid");

        var result = await _target.OpenAsync("Path", null, "/other", TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _workspaceLoadWorkflow.Verify(
            item => item.LoadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_WorkspaceCapacityReached_WHEN_OpeningWorkspace_THEN_ShouldReturnRejection()
    {
        var existing = CreateSession("ExistingId", "ExistingPath", alias: null, transaction: null);
        var second = CreateSession("SecondId", "SecondPath", alias: null, transaction: null);
        var expected = CreateResult<WorkspaceOpenOutcome>();
        SetupOpenNormalization("/workspace/New.sln", alias: null);
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(new WorkspaceHostSnapshot
        {
            Workspaces = new Dictionary<string, WorkspaceSessionSnapshot>
            {
                ["ExistingId"] = existing,
                ["SecondId"] = second,
            },
        });
        SetupRejectedErrorResult(expected, WorkspaceErrorCodes.WorkspaceCapacityReached);

        var result = await _target.OpenAsync("Path", null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Theory]
    [InlineData("/workspace/New.sln", null, "/workspace/New.sln", null)]
    [InlineData("/workspace/New.sln", "Alias", "/workspace/Existing.sln", "Alias")]
    public async Task GIVEN_DuplicateWorkspaceIdentity_WHEN_OpeningWorkspace_THEN_ShouldReturnAlreadyOpen(
        string normalizedPath,
        string? alias,
        string existingPath,
        string? existingAlias)
    {
        var existing = CreateSession("ExistingId", existingPath, existingAlias, transaction: null);
        var expected = CreateResult<WorkspaceOpenOutcome>();
        SetupOpenNormalization(normalizedPath, alias);
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(CreateHostSnapshot(existing));
        SetupRejectedErrorResult(expected, WorkspaceErrorCodes.WorkspaceAlreadyOpen);

        var result = await _target.OpenAsync("Path", alias, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_PendingRecovery_WHEN_OpeningWorkspace_THEN_ShouldRequireRecovery()
    {
        var expected = CreateResult<WorkspaceOpenOutcome>();
        SetupOpenPreflight("/workspace/New.sln", alias: null);
        _recoveryStore.Setup(item => item.GetStatusesAsync(TestContext.Current.CancellationToken)).ReturnsAsync([
            new RecoveryStatus
            {
                SolutionPath = "/workspace/New.sln",
                State = RecoveryState.RecoveryIncomplete,
            },
        ]);
        SetupRejectedResult(expected, "RecoveryPending");

        var result = await _target.OpenAsync("Path", null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Theory]
    [InlineData("/workspace/New.sln", RecoveryState.Committed)]
    [InlineData("/workspace/New.sln", RecoveryState.Restored)]
    [InlineData("/workspace/Other.sln", RecoveryState.RecoveryIncomplete)]
    public async Task GIVEN_NonBlockingRecoveryRecord_WHEN_OpeningWorkspace_THEN_ShouldContinueToLoad(
        string recoveryPath,
        RecoveryState recoveryState)
    {
        var expected = CreateResult<WorkspaceOpenOutcome>();
        SetupOpenPreflight("/workspace/New.sln", alias: null);
        _recoveryStore.Setup(item => item.GetStatusesAsync(TestContext.Current.CancellationToken)).ReturnsAsync([
            new RecoveryStatus { SolutionPath = recoveryPath, State = recoveryState },
        ]);
        SetupLoadFailure("/workspace/New.sln", ValidatedWorkspaceLoadFailure.LoadFailed);
        SetupRejectedResult(expected, WorkspaceErrorCodes.WorkspaceLoadFailed);

        var result = await _target.OpenAsync("Path", null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_SdkProjectPreflightPasses_WHEN_OpeningProject_THEN_ShouldContinueToLoad()
    {
        var expected = CreateResult<WorkspaceOpenOutcome>();
        SetupOpenPreflight("/workspace/New.csproj", alias: null);
        SetupLoadFailure("/workspace/New.csproj", ValidatedWorkspaceLoadFailure.LoadFailed);
        SetupRejectedResult(expected, WorkspaceErrorCodes.WorkspaceLoadFailed);

        var result = await _target.OpenAsync("Path", null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GIVEN_ProjectPreflightFailure_WHEN_OpeningProject_THEN_ShouldReturnExpectedRejection(bool hasDiagnostics)
    {
        var expected = CreateResult<WorkspaceOpenOutcome>();
        SetupOpenPreflight("/workspace/New.csproj", alias: null);
        SetupLoadFailure(
            "/workspace/New.csproj",
            hasDiagnostics ? ValidatedWorkspaceLoadFailure.LoadFailed : ValidatedWorkspaceLoadFailure.NotSupported,
            hasDiagnostics ? [new DiagnosticInfo { Message = "Message" }] : []);
        SetupRejectedResult(
            expected,
            hasDiagnostics ? WorkspaceErrorCodes.WorkspaceLoadFailed : WorkspaceErrorCodes.WorkspaceNotSupported);

        var result = await _target.OpenAsync("Path", null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_LoadWorkflowFails_WHEN_OpeningWorkspace_THEN_ShouldReturnLoadFailure()
    {
        var expected = CreateResult<WorkspaceOpenOutcome>();
        SetupOpenPreflight("/workspace/New.sln", alias: null);
        SetupLoadFailure("/workspace/New.sln", ValidatedWorkspaceLoadFailure.LoadFailed);
        SetupRejectedResult(expected, WorkspaceErrorCodes.WorkspaceLoadFailed);

        var result = await _target.OpenAsync("Path", null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_LoadWorkflowReturnsUnknownFailure_WHEN_OpeningWorkspace_THEN_ShouldRejectInvalidWorkflowResult()
    {
        SetupOpenPreflight("/workspace/New.sln", alias: null);
        SetupLoadFailure("/workspace/New.sln", (ValidatedWorkspaceLoadFailure)999);

        var action = async () => await _target.OpenAsync("Path", null, TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GIVEN_LoadWorkflowRejectsOutsideRoot_WHEN_OpeningWorkspace_THEN_ShouldReturnRootFailure()
    {
        var expected = CreateResult<WorkspaceOpenOutcome>();
        SetupOpenPreflight("/workspace/New.sln", alias: null);
        SetupLoadFailure("/workspace/New.sln", ValidatedWorkspaceLoadFailure.OutsideWorkspaceRoot);
        SetupRejectedResult(expected, "WorkspaceProjectOutsideRoot");

        var result = await _target.OpenAsync("Path", null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GIVEN_LoadWorkflowRejectsCompatibility_WHEN_OpeningSolution_THEN_ShouldReturnExpectedFailure(bool hasDiagnostics)
    {
        var expected = CreateResult<WorkspaceOpenOutcome>();
        SetupOpenPreflight("/workspace/New.sln", alias: null);
        SetupLoadFailure(
            "/workspace/New.sln",
            hasDiagnostics ? ValidatedWorkspaceLoadFailure.LoadFailed : ValidatedWorkspaceLoadFailure.NotSupported,
            hasDiagnostics ? [new DiagnosticInfo { Message = "Message" }] : []);
        SetupRejectedResult(
            expected,
            hasDiagnostics ? WorkspaceErrorCodes.WorkspaceLoadFailed : WorkspaceErrorCodes.WorkspaceNotSupported);

        var result = await _target.OpenAsync("Path", null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Theory]
    [InlineData("Capacity", WorkspaceErrorCodes.WorkspaceCapacityReached)]
    [InlineData("Path", WorkspaceErrorCodes.WorkspaceAlreadyOpen)]
    [InlineData("Alias", WorkspaceErrorCodes.WorkspaceAlreadyOpen)]
    public async Task GIVEN_RaceTimeValidationFailure_WHEN_OpeningWorkspace_THEN_ShouldDisposeAndReject(
        string raceKind,
        string expectedCode)
    {
        var loadedWorkspace = new Mock<ILoadedWorkspace>();
        var expected = CreateResult<WorkspaceOpenOutcome>();
        SetupOpenPreflight("/workspace/New.sln", alias: "Alias");
        SetupLoadedWorkspace("/workspace/New.sln", _workspace.CurrentSolution, loadedWorkspace);
        _sessionStore.Setup(item => item.TryAddWorkspace(
            It.IsAny<WorkspaceSessionSnapshot>(),
            It.IsAny<Func<WorkspaceHostSnapshot, WorkspaceOperationError?>>())).Returns((
                WorkspaceSessionSnapshot _,
                Func<WorkspaceHostSnapshot, WorkspaceOperationError?> validate) => validate(CreateRaceSnapshot(raceKind)));
        SetupRejectedErrorResult(expected, expectedCode);

        var result = await _target.OpenAsync("Path", "Alias", TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        loadedWorkspace.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ValidWorkspace_WHEN_OpeningWorkspace_THEN_ShouldStoreAndReturnWorkspace()
    {
        var loadedWorkspace = new Mock<ILoadedWorkspace>();
        var solution = CreateSolutionWithProject("/workspace/Project.csproj");
        var expected = CreateResult<WorkspaceOpenOutcome>();
        SetupOpenPreflight("/workspace/New.sln", "Alias");
        _workspaceLoader.Setup(item => item.NormalizeAlias(" Alias ")).Returns("Alias");
        SetupLoadedWorkspace("/workspace/New.sln", solution, loadedWorkspace);
        _sessionStore.Setup(item => item.AllocateWorkspaceId()).Returns("WorkspaceId");
        _sessionStore.Setup(item => item.AllocateWorkspaceEpoch()).Returns(2);
        _sessionStore.Setup(item => item.TryAddWorkspace(
            It.IsAny<WorkspaceSessionSnapshot>(),
            It.IsAny<Func<WorkspaceHostSnapshot, WorkspaceOperationError?>>())).Returns((WorkspaceOperationError?)null);
        _resultFactory.Setup(item => item.Succeeded(
            It.Is<WorkspaceOpenOutcome>(outcome =>
                outcome.Workspace.WorkspaceId == "WorkspaceId" && outcome.ProjectCount == 1),
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var result = await _target.OpenAsync("Path", " Alias ", TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _sessionStore.Verify(item => item.TryAddWorkspace(
            It.Is<WorkspaceSessionSnapshot>(session =>
                session.Workspace.Alias == "Alias"
                && session.Workspace.WorkspaceRoot == "/workspace"
                && session.OperationGate is WorkspaceOperationGate),
            It.IsAny<Func<WorkspaceHostSnapshot, WorkspaceOperationError?>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_Workspaces_WHEN_Listing_THEN_ShouldReturnDeterministicOrderAndOwner()
    {
        var second = CreateSession("2", "/workspace/Second.sln", alias: null, transaction: null);
        var first = CreateSession("1", "/workspace/First.sln", alias: null, transaction: null);
        var expected = CreateResult<WorkspaceListOutcome>();
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(new WorkspaceHostSnapshot
        {
            Workspaces = new Dictionary<string, WorkspaceSessionSnapshot>
            {
                ["2"] = second,
                ["1"] = first,
            },
            TransactionOwnerWorkspaceId = "2",
        });
        _resultFactory.Setup(item => item.Succeeded(
            It.Is<WorkspaceListOutcome>(outcome =>
                outcome.Workspaces.Select(workspace => workspace.WorkspaceId).SequenceEqual(new[] { "1", "2" })
                && outcome.TransactionOwnerWorkspaceId == "2"),
            null,
            null,
            null)).Returns(expected);

        var result = await _target.ListAsync(TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_NoWorkspace_WHEN_Closing_THEN_ShouldReturnWorkspaceNotOpen()
    {
        var expected = CreateResult<WorkspaceCloseOutcome>();
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(new WorkspaceHostSnapshot());
        SetupRejectedResult(expected, WorkspaceErrorCodes.WorkspaceNotOpen);

        var result = await _target.CloseAsync(null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_SelectorFailure_WHEN_Closing_THEN_ShouldReturnSelectionError()
    {
        var session = CreateSession("WorkspaceId", "Path", alias: null, transaction: null);
        var error = new WorkspaceOperationError { Code = "Code", Message = "Message" };
        var expected = CreateResult<WorkspaceCloseOutcome>();
        SetupSelectionFailure(session, error);
        SetupRejectedErrorResult(expected, "Code");

        var result = await _target.CloseAsync("WorkspaceId", null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_BusyWorkspace_WHEN_Closing_THEN_ShouldReturnWorkspaceBusy()
    {
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession("WorkspaceId", "Path", alias: null, transaction: null) with { OperationGate = gate.Object };
        var expected = CreateResult<WorkspaceCloseOutcome>();
        SetupSelection(session);
        gate.Setup(item => item.TryAcquireExclusive()).Returns((IWorkspaceOperationLease?)null);
        SetupRejectedResult(expected, WorkspaceErrorCodes.WorkspaceBusy);

        var result = await _target.CloseAsync(null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_SelectedSessionDisappears_WHEN_Closing_THEN_ShouldReturnWorkspaceNotOpen()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession("WorkspaceId", "Path", alias: null, transaction: null) with { OperationGate = gate.Object };
        var expected = CreateResult<WorkspaceCloseOutcome>();
        SetupSelection(session);
        gate.Setup(item => item.TryAcquireExclusive()).Returns(operationLease.Object);
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns((WorkspaceSessionSnapshot?)null);
        _sessionAcquirer.Setup(item => item.AcquireShared(It.IsAny<WorkspaceSelector?>()))
            .Returns(WorkspaceSessionAcquisition.Rejected(CreateError(WorkspaceErrorCodes.WorkspaceNotOpen), lease: operationLease.Object));
        SetupRejectedResult(expected, WorkspaceErrorCodes.WorkspaceNotOpen);

        var result = await _target.CloseAsync(null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Theory]
    [InlineData(WorkspaceLifecycleState.TransactionActive)]
    [InlineData(WorkspaceLifecycleState.TransactionConflicted)]
    public async Task GIVEN_OpenTransaction_WHEN_Closing_THEN_ShouldRequireCommitOrRollback(WorkspaceLifecycleState state)
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession("WorkspaceId", "Path", alias: null, CreateTransaction()) with
        {
            OperationGate = gate.Object,
            State = state,
        };
        var expected = CreateResult<WorkspaceCloseOutcome>();
        SetupSelectedSession(session, gate, operationLease, exclusive: true);
        SetupRejectedResult(expected, "TransactionOpen");

        var result = await _target.CloseAsync(null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_RemoveRace_WHEN_Closing_THEN_ShouldReturnWorkspaceNotOpen()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession("WorkspaceId", "Path", alias: null, transaction: null) with { OperationGate = gate.Object };
        var expected = CreateResult<WorkspaceCloseOutcome>();
        SetupSelectedSession(session, gate, operationLease, exclusive: true);
        _sessionStore.Setup(item => item.RemoveWorkspace("WorkspaceId")).Returns((WorkspaceSessionSnapshot?)null);
        SetupRejectedResult(expected, WorkspaceErrorCodes.WorkspaceNotOpen);

        var result = await _target.CloseAsync(null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_ReadyWorkspace_WHEN_Closing_THEN_ShouldDisposeAndReturnClosedPath()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var loadedWorkspace = new Mock<ILoadedWorkspace>();
        var session = CreateSession("WorkspaceId", "ClosedPath", alias: null, transaction: null) with
        {
            OperationGate = gate.Object,
            LoadedWorkspace = loadedWorkspace.Object,
        };
        var expected = CreateResult<WorkspaceCloseOutcome>();
        SetupSelectedSession(session, gate, operationLease, exclusive: true);
        _sessionStore.Setup(item => item.RemoveWorkspace("WorkspaceId")).Returns(session);
        _resultFactory.Setup(item => item.Succeeded(
            It.Is<WorkspaceCloseOutcome>(outcome => outcome.ClosedPath == "ClosedPath"),
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var result = await _target.CloseAsync(null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        loadedWorkspace.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_NoWorkspace_WHEN_GettingStatus_THEN_ShouldReturnWorkspaceNotOpen()
    {
        var expected = CreateResult<WorkspaceStatusOutcome>();
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(new WorkspaceHostSnapshot());
        SetupRejectedResult(expected, WorkspaceErrorCodes.WorkspaceNotOpen);

        var result = await _target.GetStatusAsync(null, null, null, StatusDetailLevel.Standard, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_SelectorFailure_WHEN_GettingStatus_THEN_ShouldReturnSelectionError()
    {
        var session = CreateSession("WorkspaceId", "Path", alias: null, transaction: null);
        var error = new WorkspaceOperationError { Code = "Code", Message = "Message" };
        var expected = CreateResult<WorkspaceStatusOutcome>();
        SetupSelectionFailure(session, error);
        SetupRejectedErrorResult(expected, "Code");

        var result = await _target.GetStatusAsync(null, "Alias", null, StatusDetailLevel.Standard, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_BusyWorkspace_WHEN_GettingStatus_THEN_ShouldReturnWorkspaceBusy()
    {
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession("WorkspaceId", "Path", alias: null, transaction: null) with { OperationGate = gate.Object };
        var expected = CreateResult<WorkspaceStatusOutcome>();
        SetupSelection(session);
        gate.Setup(item => item.TryAcquireShared()).Returns((IWorkspaceOperationLease?)null);
        SetupRejectedResult(expected, WorkspaceErrorCodes.WorkspaceBusy);

        var result = await _target.GetStatusAsync(null, null, null, StatusDetailLevel.Standard, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_SelectedSessionDisappears_WHEN_GettingStatus_THEN_ShouldReturnWorkspaceNotOpen()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession("WorkspaceId", "Path", alias: null, transaction: null) with { OperationGate = gate.Object };
        var expected = CreateResult<WorkspaceStatusOutcome>();
        SetupSelection(session);
        gate.Setup(item => item.TryAcquireShared()).Returns(operationLease.Object);
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns((WorkspaceSessionSnapshot?)null);
        _sessionAcquirer.Setup(item => item.AcquireShared(It.IsAny<WorkspaceSelector?>()))
            .Returns(WorkspaceSessionAcquisition.Rejected(CreateError(WorkspaceErrorCodes.WorkspaceNotOpen), lease: operationLease.Object));
        SetupRejectedResult(expected, WorkspaceErrorCodes.WorkspaceNotOpen);

        var result = await _target.GetStatusAsync(null, null, null, StatusDetailLevel.Standard, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Theory]
    [InlineData(WorkspaceLifecycleState.Ready, true)]
    [InlineData(WorkspaceLifecycleState.TransactionActive, true)]
    [InlineData(WorkspaceLifecycleState.WorkspaceOutOfDate, false)]
    public async Task GIVEN_WorkspaceState_WHEN_GettingStatus_THEN_ShouldApplyDetectedChangeOnlyWhenEligible(
        WorkspaceLifecycleState state,
        bool shouldCheckForChanges)
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var transaction = state == WorkspaceLifecycleState.TransactionActive ? CreateTransaction() : null;
        var session = CreateSession("WorkspaceId", "Path", alias: null, transaction) with
        {
            OperationGate = gate.Object,
            State = state,
        };
        var updatedSession = session with { State = WorkspaceLifecycleState.WorkspaceOutOfDate };
        var expected = CreateResult<WorkspaceStatusOutcome>();
        SetupSelectedSession(session, gate, operationLease, exclusive: false);
        _changeDetector.Setup(item => item.HasChanged(session.InputManifest, TestContext.Current.CancellationToken))
            .Returns(shouldCheckForChanges);
        _stateTransitions.Setup(item => item.ApplyExternalChangeDetected(session)).Returns(updatedSession);
        _resultFactory.Setup(item => item.Succeeded(
            It.Is<WorkspaceStatusOutcome>(outcome =>
                outcome.State == (shouldCheckForChanges ? WorkspaceLifecycleState.WorkspaceOutOfDate : state)),
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var result = await _target.GetStatusAsync(null, null, null, StatusDetailLevel.Standard, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _changeDetector.Verify(
            item => item.HasChanged(session.InputManifest, TestContext.Current.CancellationToken),
            shouldCheckForChanges ? Times.Once() : Times.Never());
    }

    [Theory]
    [InlineData(StatusDetailLevel.Full, true)]
    [InlineData(StatusDetailLevel.Minimal, false)]
    public async Task GIVEN_StatusDetail_WHEN_GettingStatus_THEN_ShouldProjectExpectedDiagnostics(
        StatusDetailLevel detail,
        bool includesDiagnostics)
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession("WorkspaceId", "Path", alias: null, transaction: null) with
        {
            OperationGate = gate.Object,
            LoadDiagnostics = [new DiagnosticInfo { Message = "Message" }],
        };
        var expected = CreateResult<WorkspaceStatusOutcome>();
        SetupSelectedSession(session, gate, operationLease, exclusive: false);
        _resultFactory.Setup(item => item.Succeeded(
            It.Is<WorkspaceStatusOutcome>(outcome => (outcome.LoadDiagnostics != null) == includesDiagnostics),
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var result = await _target.GetStatusAsync(null, null, null, detail, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_OtherLiveInstance_WHEN_GettingStatus_THEN_ShouldProjectItsAdvisoryState()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession("WorkspaceId", "Path", alias: null, transaction: null) with { OperationGate = gate.Object };
        var instance = new WorkspaceInstanceInfo
        {
            InstanceId = "other-instance",
            LoadedPath = "Path",
            WorkspaceRoot = "Path",
            WorkspaceState = WorkspaceLifecycleState.TransactionActive,
            TransactionRevision = 2,
            CommitPhase = "Applying",
        };
        var expected = CreateResult<WorkspaceStatusOutcome>();
        SetupSelectedSession(session, gate, operationLease, exclusive: false);
        _instanceStatusPublisher
            .Setup(item => item.GetOtherLiveInstancesAsync("Path", TestContext.Current.CancellationToken))
            .ReturnsAsync([instance]);
        _resultFactory.Setup(item => item.Succeeded(
            It.Is<WorkspaceStatusOutcome>(outcome => outcome.Instances.Count == 1 && outcome.Instances[0] == instance),
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var result = await _target.GetStatusAsync(null, null, null, StatusDetailLevel.Standard, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_NoWorkspace_WHEN_Reloading_THEN_ShouldReturnWorkspaceNotOpen()
    {
        var expected = CreateResult<WorkspaceReloadOutcome>();
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(new WorkspaceHostSnapshot());
        SetupRejectedResult(expected, WorkspaceErrorCodes.WorkspaceNotOpen);

        var result = await _target.ReloadAsync(null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_SelectorFailure_WHEN_Reloading_THEN_ShouldReturnSelectionError()
    {
        var session = CreateSession("WorkspaceId", "Path", alias: null, transaction: null);
        var error = new WorkspaceOperationError { Code = "Code", Message = "Message" };
        var expected = CreateResult<WorkspaceReloadOutcome>();
        SetupSelectionFailure(session, error);
        SetupRejectedErrorResult(expected, "Code");

        var result = await _target.ReloadAsync(null, null, "Path", TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_BusyWorkspace_WHEN_Reloading_THEN_ShouldReturnWorkspaceBusy()
    {
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession("WorkspaceId", "Path", alias: null, transaction: null) with { OperationGate = gate.Object };
        var expected = CreateResult<WorkspaceReloadOutcome>();
        SetupSelection(session);
        gate.Setup(item => item.TryAcquireExclusive()).Returns((IWorkspaceOperationLease?)null);
        SetupRejectedResult(expected, WorkspaceErrorCodes.WorkspaceBusy);

        var result = await _target.ReloadAsync(null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_SelectedSessionDisappears_WHEN_Reloading_THEN_ShouldReturnWorkspaceNotOpen()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession("WorkspaceId", "Path", alias: null, transaction: null) with { OperationGate = gate.Object };
        var expected = CreateResult<WorkspaceReloadOutcome>();
        SetupSelection(session);
        gate.Setup(item => item.TryAcquireExclusive()).Returns(operationLease.Object);
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns((WorkspaceSessionSnapshot?)null);
        _sessionAcquirer.Setup(item => item.AcquireExclusive(It.IsAny<WorkspaceSelector?>()))
            .Returns(WorkspaceSessionAcquisition.Rejected(CreateError(WorkspaceErrorCodes.WorkspaceNotOpen), lease: operationLease.Object));
        SetupRejectedResult(expected, WorkspaceErrorCodes.WorkspaceNotOpen);

        var result = await _target.ReloadAsync(null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Theory]
    [InlineData(WorkspaceLifecycleState.TransactionActive)]
    [InlineData(WorkspaceLifecycleState.TransactionConflicted)]
    public async Task GIVEN_OpenTransaction_WHEN_Reloading_THEN_ShouldReturnBlocked(WorkspaceLifecycleState state)
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession("WorkspaceId", "Path", alias: null, CreateTransaction()) with
        {
            OperationGate = gate.Object,
            State = state,
        };
        var expected = CreateResult<WorkspaceReloadOutcome>();
        SetupSelectedSession(session, gate, operationLease, exclusive: true);
        SetupRejectedResult(expected, "WorkspaceReloadBlocked");

        var result = await _target.ReloadAsync(null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_ReloadNotRequired_WHEN_Reloading_THEN_ShouldReturnRejection()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession("WorkspaceId", "Path", alias: null, transaction: null) with { OperationGate = gate.Object };
        var expected = CreateResult<WorkspaceReloadOutcome>();
        SetupSelectedSession(session, gate, operationLease, exclusive: true);
        SetupRejectedResult(expected, "WorkspaceReloadNotRequired");

        var result = await _target.ReloadAsync(null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GIVEN_ProjectCompatibilityFailure_WHEN_Reloading_THEN_ShouldReturnExpectedRejection(bool hasDiagnostics)
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession("WorkspaceId", "/workspace/Project.csproj", alias: null, transaction: null) with
        {
            OperationGate = gate.Object,
            State = WorkspaceLifecycleState.WorkspaceOutOfDate,
        };
        var expected = CreateResult<WorkspaceReloadOutcome>();
        SetupSelectedSession(session, gate, operationLease, exclusive: true);
        SetupLoadFailure(
            "/workspace/Project.csproj",
            hasDiagnostics ? ValidatedWorkspaceLoadFailure.LoadFailed : ValidatedWorkspaceLoadFailure.NotSupported,
            hasDiagnostics ? [new DiagnosticInfo { Message = "Message" }] : []);
        SetupRejectedResult(
            expected,
            hasDiagnostics ? WorkspaceErrorCodes.WorkspaceLoadFailed : WorkspaceErrorCodes.WorkspaceNotSupported);

        var result = await _target.ReloadAsync(null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_SdkProjectPreflightPasses_WHEN_Reloading_THEN_ShouldContinueToLoad()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession("WorkspaceId", "/workspace/Project.csproj", alias: null, transaction: null) with
        {
            OperationGate = gate.Object,
            State = WorkspaceLifecycleState.WorkspaceOutOfDate,
        };
        var expected = CreateResult<WorkspaceReloadOutcome>();
        SetupSelectedSession(session, gate, operationLease, exclusive: true);
        SetupLoadFailure("/workspace/Project.csproj", ValidatedWorkspaceLoadFailure.LoadFailed);
        SetupRejectedResult(expected, WorkspaceErrorCodes.WorkspaceLoadFailed);

        var result = await _target.ReloadAsync(null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_LoadWorkflowFails_WHEN_Reloading_THEN_ShouldReturnLoadFailure()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession("WorkspaceId", "/workspace/Solution.sln", alias: null, transaction: null) with
        {
            OperationGate = gate.Object,
            State = WorkspaceLifecycleState.WorkspaceOutOfDate,
        };
        var expected = CreateResult<WorkspaceReloadOutcome>();
        SetupSelectedSession(session, gate, operationLease, exclusive: true);
        SetupLoadFailure("/workspace/Solution.sln", ValidatedWorkspaceLoadFailure.LoadFailed);
        SetupRejectedResult(expected, WorkspaceErrorCodes.WorkspaceLoadFailed);

        var result = await _target.ReloadAsync(null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_ReloadedWorkspaceFallsOutsideRoot_WHEN_Reloading_THEN_ShouldReturnRootFailure()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession("WorkspaceId", "/workspace/Solution.sln", alias: null, transaction: null) with
        {
            OperationGate = gate.Object,
            State = WorkspaceLifecycleState.WorkspaceOutOfDate,
        };
        var expected = CreateResult<WorkspaceReloadOutcome>();
        SetupSelectedSession(session, gate, operationLease, exclusive: true);
        SetupLoadFailure("/workspace/Solution.sln", ValidatedWorkspaceLoadFailure.OutsideWorkspaceRoot);
        SetupRejectedResult(expected, "WorkspaceProjectOutsideRoot");

        var result = await _target.ReloadAsync(null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_OutOfDateWorkspace_WHEN_Reloading_THEN_ShouldReplaceSessionAndDisposeOldWorkspace()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var oldWorkspace = new Mock<ILoadedWorkspace>();
        var newWorkspace = new Mock<ILoadedWorkspace>();
        var solution = CreateSolutionWithProject("/workspace/Project.csproj");
        var session = CreateSession("WorkspaceId", "/workspace/Solution.sln", "Alias", transaction: null) with
        {
            OperationGate = gate.Object,
            LoadedWorkspace = oldWorkspace.Object,
            State = WorkspaceLifecycleState.WorkspaceOutOfDate,
        };
        var expected = CreateResult<WorkspaceReloadOutcome>();
        SetupSelectedSession(session, gate, operationLease, exclusive: true);
        SetupLoadedWorkspace("/workspace/Solution.sln", solution, newWorkspace);
        _changeDetector.Setup(item => item.BuildManifest(solution, "/workspace/Solution.sln"))
            .Returns(new WorkspaceInputManifest());
        _sessionStore.Setup(item => item.AllocateWorkspaceEpoch()).Returns(3);
        _sessionStore.SetupSequence(item => item.ReadSession("WorkspaceId")).Returns(session).Returns(session);
        _resultFactory.Setup(item => item.Succeeded(
            It.Is<WorkspaceReloadOutcome>(outcome =>
                outcome.Workspace.WorkspaceId == "WorkspaceId" && outcome.Workspace.WorkspaceEpoch == 3),
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var result = await _target.ReloadAsync(null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        oldWorkspace.Verify(item => item.Dispose(), Times.Once);
        _sessionStore.Verify(item => item.ReplaceSession(It.Is<WorkspaceSessionSnapshot>(replacement =>
            replacement.LoadedWorkspace == newWorkspace.Object
            && replacement.OperationGate == gate.Object
            && replacement.Workspace.Alias == "Alias")), Times.Once);
    }

    [Fact]
    public async Task GIVEN_OldSessionDisappearsAfterReload_WHEN_Reloading_THEN_ShouldStillStoreReloadedSession()
    {
        var operationLease = new Mock<IWorkspaceOperationLease>();
        var gate = new Mock<IWorkspaceOperationGate>();
        var newWorkspace = new Mock<ILoadedWorkspace>();
        var session = CreateSession("WorkspaceId", "/workspace/Solution.sln", alias: null, transaction: null) with
        {
            OperationGate = gate.Object,
            State = WorkspaceLifecycleState.WorkspaceOutOfDate,
        };
        var expected = CreateResult<WorkspaceReloadOutcome>();
        SetupSelection(session);
        gate.Setup(item => item.TryAcquireExclusive()).Returns(operationLease.Object);
        _sessionStore.SetupSequence(item => item.ReadSession("WorkspaceId"))
            .Returns(session)
            .Returns((WorkspaceSessionSnapshot?)null);
        SetupLoadedWorkspace("/workspace/Solution.sln", _workspace.CurrentSolution, newWorkspace);
        _changeDetector.Setup(item => item.BuildManifest(_workspace.CurrentSolution, "/workspace/Solution.sln"))
            .Returns(new WorkspaceInputManifest());
        _resultFactory.Setup(item => item.Succeeded(
            It.IsAny<WorkspaceReloadOutcome>(),
            It.IsAny<WorkspaceOperationContext>(),
            null,
            null)).Returns(expected);

        var result = await _target.ReloadAsync(null, null, null, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _sessionStore.Verify(item => item.ReplaceSession(It.IsAny<WorkspaceSessionSnapshot>()), Times.Once);
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    private void SetupOpenNormalization(string normalizedPath, string? alias)
    {
        _workspaceLoader.Setup(item => item.NormalizeOpenPath("Path")).Returns(normalizedPath);
        _workspaceLoader.Setup(item => item.NormalizeAlias(alias)).Returns(alias?.Trim());
    }

    private void SetupOpenPreflight(string normalizedPath, string? alias)
    {
        SetupOpenNormalization(normalizedPath, alias);
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(new WorkspaceHostSnapshot());
        _recoveryStore.Setup(item => item.GetStatusesAsync(TestContext.Current.CancellationToken)).ReturnsAsync([]);
    }

    private void SetupLoadedWorkspace(string path, Solution solution, Mock<ILoadedWorkspace> loadedWorkspace)
    {
        loadedWorkspace.SetupGet(item => item.CurrentSolution).Returns(solution);
        _workspaceLoadWorkflow.Setup(item => item.LoadAsync(path, It.IsAny<string>(), TestContext.Current.CancellationToken))
            .ReturnsAsync(ValidatedWorkspaceLoadResult.Succeeded(loadedWorkspace.Object, solution, []));
        _changeDetector.Setup(item => item.BuildManifest(solution, path)).Returns(new WorkspaceInputManifest());
    }

    private void SetupLoadFailure(
        string path,
        ValidatedWorkspaceLoadFailure failure,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null)
    {
        _workspaceLoadWorkflow.Setup(item => item.LoadAsync(path, It.IsAny<string>(), TestContext.Current.CancellationToken))
            .ReturnsAsync(ValidatedWorkspaceLoadResult.Failed(failure, diagnostics));
    }

    private void SetupSelection(WorkspaceSessionSnapshot session)
    {
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(CreateHostSnapshot(session));
        _sessionAcquirer.Setup(item => item.AcquireShared(It.IsAny<WorkspaceSelector?>()))
            .Returns(() => CreateAcquisition(session, exclusive: false));
        _sessionAcquirer.Setup(item => item.AcquireExclusive(It.IsAny<WorkspaceSelector?>()))
            .Returns(() => CreateAcquisition(session, exclusive: true));
    }

    private void SetupSelectionFailure(WorkspaceSessionSnapshot session, WorkspaceOperationError error)
    {
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(CreateHostSnapshot(session));
        _sessionAcquirer.Setup(item => item.AcquireShared(It.IsAny<WorkspaceSelector?>()))
            .Returns(WorkspaceSessionAcquisition.Rejected(error));
        _sessionAcquirer.Setup(item => item.AcquireExclusive(It.IsAny<WorkspaceSelector?>()))
            .Returns(WorkspaceSessionAcquisition.Rejected(error));
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

    private void SetupSelectedSession(
        WorkspaceSessionSnapshot session,
        Mock<IWorkspaceOperationGate> gate,
        Mock<IWorkspaceOperationLease> operationLease,
        bool exclusive)
    {
        SetupSelection(session);
        if (exclusive)
        {
            gate.Setup(item => item.TryAcquireExclusive()).Returns(operationLease.Object);
        }
        else
        {
            gate.Setup(item => item.TryAcquireShared()).Returns(operationLease.Object);
        }

        _sessionStore.Setup(item => item.ReadSession(session.Workspace.WorkspaceId)).Returns(session);
    }

    private void SetupRejectedResult<TOutcome>(WorkspaceOperationResult<TOutcome> result, string code)
    {
        _resultFactory.Setup(item => item.Rejected<TOutcome>(
            code,
            It.IsAny<string>(),
            It.IsAny<RequiredAction?>(),
            It.IsAny<WorkspaceOperationContext?>(),
            It.IsAny<IReadOnlyList<DiagnosticInfo>?>(),
            null)).Returns(result);
        _resultFactory.Setup(item => item.Rejected<TOutcome>(
            It.Is<WorkspaceOperationError>(error => error.Code == code),
            It.IsAny<WorkspaceOperationContext?>(),
            null,
            null)).Returns(result);
    }

    private void SetupRejectedErrorResult<TOutcome>(WorkspaceOperationResult<TOutcome> result, string code)
    {
        _resultFactory.Setup(item => item.Rejected<TOutcome>(
            It.Is<WorkspaceOperationError>(error => error.Code == code),
            null,
            null,
            null)).Returns(result);
    }

    private WorkspaceSessionSnapshot CreateSession(
        string workspaceId,
        string path,
        string? alias,
        WorkspaceTransaction? transaction)
    {
        return new WorkspaceSessionSnapshot
        {
            State = transaction is null ? WorkspaceLifecycleState.Ready : WorkspaceLifecycleState.TransactionActive,
            Workspace = new WorkspaceIdentity
            {
                WorkspaceId = workspaceId,
                WorkspaceEpoch = 2,
                LoadedPath = path,
                WorkspaceRoot = path,
                Alias = alias,
            },
            LoadedWorkspace = new Mock<ILoadedWorkspace>().Object,
            CurrentSolution = transaction?.CurrentSolution ?? _workspace.CurrentSolution,
            Transaction = transaction,
            InputManifest = new WorkspaceInputManifest(),
            OperationGate = new Mock<IWorkspaceOperationGate>().Object,
        };
    }

    private Solution CreateSolutionWithProject(string projectPath)
    {
        return _workspace.CurrentSolution.AddProject(ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            "Project",
            "Project",
            LanguageNames.CSharp,
            filePath: projectPath));
    }

    private WorkspaceTransaction CreateTransaction()
    {
        return new WorkspaceTransaction
        {
            BaselineSolution = _workspace.CurrentSolution,
            MaxRevisions = 3,
        };
    }

    private static WorkspaceHostSnapshot CreateHostSnapshot(WorkspaceSessionSnapshot session)
    {
        return new WorkspaceHostSnapshot
        {
            Workspaces = new Dictionary<string, WorkspaceSessionSnapshot>
            {
                [session.Workspace.WorkspaceId] = session,
            },
        };
    }

    private WorkspaceHostSnapshot CreateRaceSnapshot(string raceKind)
    {
        var first = CreateSession(
            "ExistingId",
            raceKind == "Path" ? "/workspace/New.sln" : "/workspace/Existing.sln",
            raceKind == "Alias" ? "Alias" : null,
            transaction: null);
        var workspaces = new Dictionary<string, WorkspaceSessionSnapshot>
        {
            ["ExistingId"] = first,
        };
        if (raceKind == "Capacity")
        {
            workspaces["SecondId"] = CreateSession("SecondId", "/workspace/Second.sln", alias: null, transaction: null);
        }

        return new WorkspaceHostSnapshot { Workspaces = workspaces };
    }

    private static WorkspaceOperationResult<TOutcome> CreateResult<TOutcome>()
    {
        return new WorkspaceOperationResult<TOutcome>();
    }
}
