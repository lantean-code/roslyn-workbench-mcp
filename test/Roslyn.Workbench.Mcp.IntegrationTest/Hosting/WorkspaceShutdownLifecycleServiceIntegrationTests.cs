using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Workspace.Caching;
using Roslyn.Workbench.Mcp.Workspace.ChangeDetection;
using Roslyn.Workbench.Mcp.Workspace.Coordination;
using Roslyn.Workbench.Mcp.Workspace.Operations;
using Roslyn.Workbench.Mcp.Workspace.Paths;
using Roslyn.Workbench.Mcp.Workspace.Selection;
using Roslyn.Workbench.Mcp.Workspace.State;

namespace Roslyn.Workbench.Mcp.Test.Hosting;

public sealed class WorkspaceShutdownLifecycleServiceIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_TransportAndWorkspaceLifecycle_WHEN_StoppingHost_THEN_ShouldStopTransportBeforeWorkspaces()
    {
        var sequence = new MockSequence();
        var workspaceLifecycleService = new Mock<IWorkspaceLifecycleService>(MockBehavior.Strict);
        var transport = new Mock<IHostedService>(MockBehavior.Strict);
        transport
            .Setup(item => item.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        transport
            .InSequence(sequence)
            .Setup(item => item.StopAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        workspaceLifecycleService
            .InSequence(sequence)
            .Setup(item => item.ShutdownAsync())
            .Returns(ValueTask.CompletedTask);

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(workspaceLifecycleService.Object);
        builder.Services.AddHostedService<WorkspaceShutdownLifecycleService>();
        builder.Services.AddSingleton(transport.Object);
        using var host = builder.Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        workspaceLifecycleService.Verify(item => item.ShutdownAsync(), Times.Once);
        transport.Verify(item => item.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_OpenWorkspaceResources_WHEN_StoppingHost_THEN_ShouldDrainAndDisposeSession()
    {
        using var workspace = new AdhocWorkspace();
        var inputChangeMonitor = new Mock<IWorkspaceInputChangeMonitor>();
        var loadedWorkspace = new Mock<ILoadedWorkspace>();
        var operationGate = new Mock<IWorkspaceOperationGate>();
        var queryCache = new Mock<IWorkspaceQueryCache>();
        var sessionAcquirer = new Mock<IWorkspaceSessionAcquirer>();
        var workspaceLoader = new Mock<IWorkspaceLoader>();
        var workspaceRootResolver = new Mock<IWorkspaceRootResolver>();
        var workspacePathComparison = new Mock<IWorkspacePathComparison>();
        var workspaceLoadWorkflow = new Mock<IWorkspaceLoadWorkflow>();
        var workspaceChangeDetector = new Mock<IWorkspaceChangeDetector>();
        var workspaceStateTransitions = new Mock<IWorkspaceStateTransitions>();
        var resultFactory = new Mock<IWorkspaceOperationResultFactory>();
        var recoveryStore = new Mock<ICommitRecoveryStore>();
        var instanceStatusPublisher = new Mock<IWorkspaceInstanceStatusPublisher>();
        var workspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceId = workspaceId,
            WorkspaceEpoch = 1,
            LoadedPath = "LoadedPath",
            WorkspaceRoot = "WorkspaceRoot",
        };

        var snapshotId = new WorkspaceSnapshotId(1);
        var inputManifest = new WorkspaceInputManifest
        {
            ChangeMonitor = inputChangeMonitor.Object,
        };

        var session = new WorkspaceSessionSnapshot
        {
            CommittedSnapshotId = snapshotId,
            State = WorkspaceLifecycleState.Ready,
            Workspace = workspaceIdentity,
            LoadedWorkspace = loadedWorkspace.Object,
            CurrentSolution = workspace.CurrentSolution,
            InputManifest = inputManifest,
            OperationGate = operationGate.Object,
            CurrentSnapshotIdentity = WorkspaceSnapshotIdentity.Create(workspaceIdentity, snapshotId, transaction: null),
        };

        var sessionStore = new WorkspaceSessionStore(queryCache.Object, []);
        var sessionCleanup = new WorkspaceSessionCleanup(instanceStatusPublisher.Object);
        var workspaceOptions = Options.Create(new WorkspaceOptions());
        var workspaceLifecycleService = new WorkspaceLifecycleService(
            workspaceOptions,
            sessionStore,
            sessionAcquirer.Object,
            workspaceLoader.Object,
            workspaceRootResolver.Object,
            workspacePathComparison.Object,
            workspaceLoadWorkflow.Object,
            workspaceChangeDetector.Object,
            workspaceStateTransitions.Object,
            resultFactory.Object,
            recoveryStore.Object,
            instanceStatusPublisher.Object,
            sessionCleanup);

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<IWorkspaceLifecycleService>(workspaceLifecycleService);
        builder.Services.AddHostedService<WorkspaceShutdownLifecycleService>();
        using var host = builder.Build();
        var addError = sessionStore.TryAddWorkspace(session, static _ => null);
        addError.Should().BeNull();

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        sessionStore.ReadSnapshot().Workspaces.Should().BeEmpty();
        inputChangeMonitor.Verify(item => item.Dispose(), Times.Once);
        loadedWorkspace.Verify(item => item.Dispose(), Times.Once);
    }
}
