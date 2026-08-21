using Roslyn.Workbench.Mcp.Workspace.ChangeDetection;
using Roslyn.Workbench.Mcp.Workspace.Coordination;
using Roslyn.Workbench.Mcp.Workspace.Lifecycle;
using Roslyn.Workbench.Mcp.Workspace.Loading;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Lifecycle;

public sealed class WorkspaceSessionCleanupTests : IDisposable
{
    private readonly AdhocWorkspace _workspace = new();

    [Fact]
    public async Task GIVEN_OpenSession_WHEN_CleaningUp_THEN_ShouldCloseStatusAndDisposeOwnedResources()
    {
        var instanceStatusPublisher = new Mock<IWorkspaceInstanceStatusPublisher>();
        var inputChangeMonitor = new Mock<IWorkspaceInputChangeMonitor>();
        var loadedWorkspace = new Mock<ILoadedWorkspace>();
        var session = CreateSession(inputChangeMonitor, loadedWorkspace);
        var target = new WorkspaceSessionCleanup(instanceStatusPublisher.Object);

        await target.CleanupAsync(session);

        instanceStatusPublisher.Verify(item => item.CloseAsync(session.Workspace.WorkspaceId), Times.Once);
        inputChangeMonitor.Verify(item => item.Dispose(), Times.Once);
        loadedWorkspace.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_StatusCloseFails_WHEN_CleaningUp_THEN_ShouldDisposeOwnedResourcesAndPropagateFailure()
    {
        var instanceStatusPublisher = new Mock<IWorkspaceInstanceStatusPublisher>();
        var inputChangeMonitor = new Mock<IWorkspaceInputChangeMonitor>();
        var loadedWorkspace = new Mock<ILoadedWorkspace>();
        var session = CreateSession(inputChangeMonitor, loadedWorkspace);
        var target = new WorkspaceSessionCleanup(instanceStatusPublisher.Object);
        instanceStatusPublisher
            .Setup(item => item.CloseAsync(session.Workspace.WorkspaceId))
            .Returns(() => ValueTask.FromException(new InvalidOperationException("Failure")));

        var action = async () => await target.CleanupAsync(session);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("Failure");
        inputChangeMonitor.Verify(item => item.Dispose(), Times.Once);
        loadedWorkspace.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ManifestDisposalFails_WHEN_CleaningUp_THEN_ShouldDisposeWorkspaceAndPropagateFailure()
    {
        var instanceStatusPublisher = new Mock<IWorkspaceInstanceStatusPublisher>();
        var inputChangeMonitor = new Mock<IWorkspaceInputChangeMonitor>();
        var loadedWorkspace = new Mock<ILoadedWorkspace>();
        var session = CreateSession(inputChangeMonitor, loadedWorkspace);
        var target = new WorkspaceSessionCleanup(instanceStatusPublisher.Object);
        inputChangeMonitor.Setup(item => item.Dispose()).Throws(new InvalidOperationException("Failure"));

        var action = async () => await target.CleanupAsync(session);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("Failure");
        loadedWorkspace.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_WorkspaceDisposalFails_WHEN_CleaningUp_THEN_ShouldPropagateFailureAfterOtherCleanup()
    {
        var instanceStatusPublisher = new Mock<IWorkspaceInstanceStatusPublisher>();
        var inputChangeMonitor = new Mock<IWorkspaceInputChangeMonitor>();
        var loadedWorkspace = new Mock<ILoadedWorkspace>();
        var session = CreateSession(inputChangeMonitor, loadedWorkspace);
        var target = new WorkspaceSessionCleanup(instanceStatusPublisher.Object);
        loadedWorkspace.Setup(item => item.Dispose()).Throws(new InvalidOperationException("Failure"));

        var action = async () => await target.CleanupAsync(session);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("Failure");
        instanceStatusPublisher.Verify(item => item.CloseAsync(session.Workspace.WorkspaceId), Times.Once);
        inputChangeMonitor.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_MultipleCleanupOperationsFail_WHEN_CleaningUp_THEN_ShouldReportEveryFailure()
    {
        var instanceStatusPublisher = new Mock<IWorkspaceInstanceStatusPublisher>();
        var inputChangeMonitor = new Mock<IWorkspaceInputChangeMonitor>();
        var loadedWorkspace = new Mock<ILoadedWorkspace>();
        var session = CreateSession(inputChangeMonitor, loadedWorkspace);
        var target = new WorkspaceSessionCleanup(instanceStatusPublisher.Object);
        instanceStatusPublisher
            .Setup(item => item.CloseAsync(session.Workspace.WorkspaceId))
            .Returns(() => ValueTask.FromException(new InvalidOperationException("StatusFailure")));

        inputChangeMonitor.Setup(item => item.Dispose()).Throws(new IOException("ManifestFailure"));
        loadedWorkspace.Setup(item => item.Dispose()).Throws(new UnauthorizedAccessException("WorkspaceFailure"));

        var action = async () => await target.CleanupAsync(session);

        var assertion = await action.Should().ThrowAsync<AggregateException>();
        assertion.Which.InnerExceptions.Should().HaveCount(3);
        assertion.Which.InnerExceptions.Should().ContainSingle(exception => exception is InvalidOperationException && exception.Message == "StatusFailure");
        assertion.Which.InnerExceptions.Should().ContainSingle(exception => exception is IOException && exception.Message == "ManifestFailure");
        assertion.Which.InnerExceptions.Should().ContainSingle(exception => exception is UnauthorizedAccessException && exception.Message == "WorkspaceFailure");
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    private WorkspaceSessionSnapshot CreateSession(
        Mock<IWorkspaceInputChangeMonitor> inputChangeMonitor,
        Mock<ILoadedWorkspace> loadedWorkspace)
    {
        var workspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceId = workspaceId,
            WorkspaceEpoch = 1,
            LoadedPath = "LoadedPath",
            WorkspaceRoot = "WorkspaceRoot",
        };

        var snapshotId = WorkspaceSnapshotTestFactory.CreateId(1);
        var inputManifest = new WorkspaceInputManifest
        {
            ChangeMonitor = inputChangeMonitor.Object,
        };

        var operationGate = new Mock<IWorkspaceOperationGate>();
        return new WorkspaceSessionSnapshot
        {
            CommittedSnapshotId = snapshotId,
            State = WorkspaceLifecycleState.Ready,
            Workspace = workspaceIdentity,
            LoadedWorkspace = loadedWorkspace.Object,
            CurrentSolution = _workspace.CurrentSolution,
            InputManifest = inputManifest,
            OperationGate = operationGate.Object,
            CurrentSnapshotIdentity = WorkspaceSnapshotIdentity.Create(workspaceIdentity, snapshotId, transaction: null),
        };
    }
}
