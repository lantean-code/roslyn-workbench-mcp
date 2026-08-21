using Roslyn.Workbench.Mcp.Workspace.ChangeDetection;
using Roslyn.Workbench.Mcp.Workspace.Loading;
using Roslyn.Workbench.Mcp.Workspace.Selection;

namespace Roslyn.Workbench.Mcp.Workspace.Test.State;

public sealed class WorkspaceSessionAcquirerTests : IDisposable
{
    private readonly AdhocWorkspace _workspace;
    private readonly Mock<IWorkspaceSessionStore> _sessionStore;
    private readonly Mock<IWorkspaceSelector> _workspaceSelector;
    private readonly WorkspaceSessionAcquirer _target;

    public WorkspaceSessionAcquirerTests()
    {
        _workspace = new AdhocWorkspace();
        _sessionStore = new Mock<IWorkspaceSessionStore>();
        _workspaceSelector = new Mock<IWorkspaceSelector>();
        _target = new WorkspaceSessionAcquirer(_sessionStore.Object, _workspaceSelector.Object);
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GIVEN_NoLoadedWorkspace_WHEN_Acquiring_THEN_ShouldReturnWorkspaceRequired(bool exclusive)
    {
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(new WorkspaceHostSnapshot());

        var result = Acquire(exclusive);

        result.HasError.Should().BeTrue();
        var error = result.Error.Should().BeOfType<WorkspaceOperationError>().Which;
        error.Code.Should().Be(WorkspaceErrorCodes.WorkspaceNotOpen);
        error.RequiredAction.Should().Be(RequiredAction.OpenWorkspace);
        result.ContextSession.Should().BeNull();
        result.Lease.Should().BeNull();
        _workspaceSelector.Verify(item => item.Select(It.IsAny<WorkspaceHostSnapshot>(), It.IsAny<WorkspaceSelector?>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GIVEN_SelectorFailure_WHEN_Acquiring_THEN_ShouldPreserveError(bool exclusive)
    {
        var gate = new Mock<IWorkspaceOperationGate>();
        var snapshot = CreateSnapshot(CreateSession(gate.Object));
        var error = new WorkspaceOperationError { Code = "Code", Message = "Message" };
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(snapshot);
        _workspaceSelector.Setup(item => item.Select(snapshot, null)).Returns(WorkspaceSelectionResult.Failure(error));

        var result = Acquire(exclusive);

        result.HasError.Should().BeTrue();
        result.Error.Should().BeSameAs(error);
        result.ContextSession.Should().BeNull();
        result.Lease.Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GIVEN_GateIsBusy_WHEN_Acquiring_THEN_ShouldReturnBusyWithSelectedContext(bool exclusive)
    {
        var gate = new Mock<IWorkspaceOperationGate>();
        var session = CreateSession(gate.Object);
        SetupSelection(session);
        if (exclusive)
        {
            gate.Setup(item => item.TryAcquireExclusive()).Returns((IWorkspaceOperationLease?)null);
        }
        else
        {
            gate.Setup(item => item.TryAcquireShared()).Returns((IWorkspaceOperationLease?)null);
        }

        var result = Acquire(exclusive);

        result.HasError.Should().BeTrue();
        var error = result.Error.Should().BeOfType<WorkspaceOperationError>().Which;
        error.Code.Should().Be(WorkspaceErrorCodes.WorkspaceBusy);
        error.RequiredAction.Should().Be(RequiredAction.Retry);
        result.ContextSession.Should().BeSameAs(session);
        result.Lease.Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GIVEN_SelectedSessionDisappears_WHEN_Acquiring_THEN_ShouldRetainLeaseForCallerDisposal(bool exclusive)
    {
        var gate = new Mock<IWorkspaceOperationGate>();
        var lease = new Mock<IWorkspaceOperationLease>();
        var session = CreateSession(gate.Object);
        SetupSelection(session);
        SetupLease(gate, lease.Object, exclusive);
        _sessionStore.Setup(item => item.ReadSession(Guid.Parse("11111111-1111-1111-1111-111111111111"))).Returns((WorkspaceSessionSnapshot?)null);

        var result = Acquire(exclusive);

        result.HasError.Should().BeTrue();
        var error = result.Error.Should().BeOfType<WorkspaceOperationError>().Which;
        error.Code.Should().Be(WorkspaceErrorCodes.WorkspaceNotOpen);
        result.Lease.Should().BeSameAs(lease.Object);
        result.ContextSession.Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GIVEN_SelectedSessionRemains_WHEN_Acquiring_THEN_ShouldReturnRefreshedSessionAndLease(bool exclusive)
    {
        var gate = new Mock<IWorkspaceOperationGate>();
        var lease = new Mock<IWorkspaceOperationLease>();
        var selectedSession = CreateSession(gate.Object);
        var refreshedSession = selectedSession with { State = WorkspaceLifecycleState.TransactionActive };
        SetupSelection(selectedSession);
        SetupLease(gate, lease.Object, exclusive);
        _sessionStore.Setup(item => item.ReadSession(Guid.Parse("11111111-1111-1111-1111-111111111111"))).Returns(refreshedSession);

        var result = Acquire(exclusive);

        result.HasError.Should().BeFalse();
        result.Error.Should().BeNull();
        var selection = result.Selection.Should().BeOfType<WorkspaceSelection>().Which;
        selection.WorkspaceId.Should().Be(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        selection.Session.Should().BeSameAs(refreshedSession);
        result.Session.Should().BeSameAs(refreshedSession);
        result.ContextSession.Should().BeSameAs(refreshedSession);
        result.Lease.Should().BeSameAs(lease.Object);
    }

    private WorkspaceSessionAcquisition Acquire(bool exclusive)
    {
        if (exclusive)
        {
            return _target.AcquireExclusive(selector: null);
        }

        return _target.AcquireShared(selector: null);
    }

    private void SetupSelection(WorkspaceSessionSnapshot session)
    {
        var snapshot = CreateSnapshot(session);
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(snapshot);
        _workspaceSelector.Setup(item => item.Select(snapshot, null)).Returns(WorkspaceSelectionResult.Success(new WorkspaceSelection
        {
            WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Session = session,
        }));
    }

    private static void SetupLease(Mock<IWorkspaceOperationGate> gate, IWorkspaceOperationLease lease, bool exclusive)
    {
        if (exclusive)
        {
            gate.Setup(item => item.TryAcquireExclusive()).Returns(lease);
        }
        else
        {
            gate.Setup(item => item.TryAcquireShared()).Returns(lease);
        }
    }

    private static WorkspaceHostSnapshot CreateSnapshot(WorkspaceSessionSnapshot session)
    {
        return new WorkspaceHostSnapshot
        {
            Workspaces = new Dictionary<Guid, WorkspaceSessionSnapshot> { [Guid.Parse("11111111-1111-1111-1111-111111111111")] = session },
        };
    }

    private WorkspaceSessionSnapshot CreateSession(IWorkspaceOperationGate gate)
    {
        var workspace = new Mock<ILoadedWorkspace>();
        var committedSnapshotId = WorkspaceSnapshotTestFactory.CreateId(1);
        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            LoadedPath = "LoadedPath",
        };

        return new WorkspaceSessionSnapshot
        {
            CommittedSnapshotId = committedSnapshotId,
            State = WorkspaceLifecycleState.Ready,
            Workspace = workspaceIdentity,
            LoadedWorkspace = workspace.Object,
            CurrentSolution = _workspace.CurrentSolution,
            InputManifest = new WorkspaceInputManifest(),
            OperationGate = gate,
            CurrentSnapshotIdentity = WorkspaceSnapshotIdentity.Create(
                workspaceIdentity,
                committedSnapshotId,
                transaction: null),
        };
    }
}
