using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Workspace.Test;

public sealed class WorkspaceLifecycleIntegrationTests
{
    [Fact]
    public async Task GIVEN_UnloadedCoordinator_WHEN_OpeningWorkspace_THEN_ShouldTransitionToReady()
    {
        await using var fixture = await TestWorkspaceFixture.CreateAsync();
        await using var target = fixture.CreateCoordinator();

        var result = await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data.Should().NotBeNull();
        result.Data!.Workspace.Should().NotBeNull();
        result.Data.ProjectCount.Should().Be(1);
        result.Data.DocumentCount.Should().BeGreaterThan(0);

        var status = await target.GetStatusAsync(new WorkspaceStatusRequest(), TestContext.Current.CancellationToken);

        status.Data!.State.Should().Be(WorkspaceLifecycleState.Ready);
        status.Data.Workspace!.LoadedPath.Should().Be(fixture.ProjectPath);
        status.WorkspaceEpoch.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GIVEN_ReadyCoordinator_WHEN_ClosingWorkspace_THEN_ShouldTransitionToUnloaded()
    {
        await using var fixture = await TestWorkspaceFixture.CreateAsync();
        await using var target = fixture.CreateCoordinator();
        await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, TestContext.Current.CancellationToken);

        var result = await target.CloseAsync(new WorkspaceCloseRequest(), TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.ClosedPath.Should().Be(fixture.ProjectPath);

        var status = await target.GetStatusAsync(new WorkspaceStatusRequest(), TestContext.Current.CancellationToken);

        status.Outcome.Should().Be(ToolOutcome.Rejected);
        status.Error!.Code.Should().Be("WorkspaceNotOpen");
    }

    [Fact]
    public async Task GIVEN_AnotherLiveServerInstance_WHEN_OpeningAndQueryingStatus_THEN_ShouldSurfaceItsAdvisoryState()
    {
        await using var fixture = await TestWorkspaceFixture.CreateAsync();
        await using var firstStateDirectory = TemporaryDirectory.Create("roslyn-workbench-mcp-state-tests");
        await using var secondStateDirectory = TemporaryDirectory.Create("roslyn-workbench-mcp-state-tests");
        await using var first = WorkspaceCoordinatorFactory.Create(new WorkspaceRuntimeOptions
        {
            StateDirectory = firstStateDirectory.DirectoryPath,
        });
        await using var second = WorkspaceCoordinatorFactory.Create(new WorkspaceRuntimeOptions
        {
            StateDirectory = secondStateDirectory.DirectoryPath,
        });
        await first.OpenAsync(new WorkspaceOpenRequest { Path = fixture.ProjectPath }, TestContext.Current.CancellationToken);

        var secondOpen = await second.OpenAsync(new WorkspaceOpenRequest { Path = fixture.ProjectPath }, TestContext.Current.CancellationToken);
        var status = await second.GetStatusAsync(new WorkspaceStatusRequest(), TestContext.Current.CancellationToken);

        secondOpen.Outcome.Should().Be(ToolOutcome.Succeeded);
        secondOpen.Data!.LoadDiagnostics.Should().Contain(diagnostic => diagnostic.Id == "WorkspaceInUse");
        status.Data!.Instances.Should().ContainSingle();
        status.Data.Instances[0].LoadedPath.Should().Be(fixture.ProjectPath);
        status.Data.Instances[0].WorkspaceState.Should().Be(WorkspaceLifecycleState.Ready);

        await second.CloseAsync(new WorkspaceCloseRequest(), TestContext.Current.CancellationToken);
        await first.CloseAsync(new WorkspaceCloseRequest(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GIVEN_IndependentDefaultRuntimes_WHEN_CreatedAndDisposed_THEN_ShouldUseAndDeleteUniqueStateDirectories()
    {
        string firstStateDirectory;
        string secondStateDirectory;
        await using (var first = WorkspaceCoordinatorFactory.Create())
        {
            await using (var second = WorkspaceCoordinatorFactory.Create())
            {
                firstStateDirectory = first.StateDirectory;
                secondStateDirectory = second.StateDirectory;

                firstStateDirectory.Should().NotBe(secondStateDirectory);
                Directory.Exists(firstStateDirectory).Should().BeTrue();
                Directory.Exists(secondStateDirectory).Should().BeTrue();
            }
        }

        Directory.Exists(firstStateDirectory).Should().BeFalse();
        Directory.Exists(secondStateDirectory).Should().BeFalse();
    }


    [Fact]
    public async Task GIVEN_TwoOpenedWorkspaces_WHEN_ListingAndGettingStatus_THEN_ShouldRequireExplicitSelection()
    {
        await using var fixtureA = await TestWorkspaceFixture.CreateAsync();
        await using var fixtureB = await TestWorkspaceFixture.CreateAsync();
        await using var target = fixtureA.CreateCoordinator();

        var openA = await target.OpenAsync(new WorkspaceOpenRequest
        {
            Alias = "alpha",
            Path = fixtureA.ProjectPath,
        }, TestContext.Current.CancellationToken);
        var openB = await target.OpenAsync(new WorkspaceOpenRequest
        {
            Alias = "beta",
            Path = fixtureB.ProjectPath,
        }, TestContext.Current.CancellationToken);

        var list = await target.ListAsync(new WorkspaceListRequest(), TestContext.Current.CancellationToken);
        var ambiguousStatus = await target.GetStatusAsync(new WorkspaceStatusRequest(), TestContext.Current.CancellationToken);
        var selectedStatus = await target.GetStatusAsync(new WorkspaceStatusRequest
        {
            Workspace = new WorkspaceSelector
            {
                WorkspaceId = openB.Data!.Workspace!.WorkspaceId,
            },
        }, TestContext.Current.CancellationToken);

        list.Outcome.Should().Be(ToolOutcome.Succeeded);
        list.Data!.Workspaces.Should().HaveCount(2);
        list.Data.Workspaces.Select(static workspace => workspace.WorkspaceId).Should().Contain([openA.Data!.Workspace!.WorkspaceId, openB.Data!.Workspace!.WorkspaceId]);
        ambiguousStatus.Outcome.Should().Be(ToolOutcome.Rejected);
        ambiguousStatus.Error!.Code.Should().Be("WorkspaceSelectorRequired");
        selectedStatus.Outcome.Should().Be(ToolOutcome.Succeeded);
        selectedStatus.Data!.Workspace!.WorkspaceId.Should().Be(openB.Data!.Workspace!.WorkspaceId);
        selectedStatus.Data.Workspace.Alias.Should().Be("beta");
    }

    [Fact]
    public async Task GIVEN_NonSdkStyleProject_WHEN_OpeningWorkspace_THEN_ShouldRejectRequest()
    {
        await using var fixture = await TestWorkspaceFixture.CreateLegacyProjectAsync();
        await using var target = fixture.CreateCoordinator();

        var result = await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("WorkspaceNotSupported");
    }

    [Fact]
    public async Task GIVEN_MalformedProject_WHEN_OpeningWorkspace_THEN_ShouldReturnStructuredLoadDiagnostics()
    {
        await using var fixture = await TestWorkspaceFixture.CreateMalformedProjectAsync();
        await using var target = fixture.CreateCoordinator();

        var result = await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("WorkspaceLoadFailed");
        result.Diagnostics.Should().NotBeEmpty();
    }


    [Fact]
    public async Task GIVEN_UnresolvedRecoveryState_WHEN_OpeningWorkspace_THEN_ShouldRejectRequest()
    {
        await using var fixture = await TestWorkspaceFixture.CreateAsync();
        await using var stateDirectory = TemporaryDirectory.Create("roslyn-workbench-mcp-recovery-tests");
        var fileSystem = new FileSystem();
        var recoveryStore = new CommitRecoveryStore(
            Options.Create(new WorkspaceCoordinatorOptions { StateDirectory = stateDirectory.DirectoryPath }),
            fileSystem,
            new AtomicFileWriter(fileSystem, new NativeAtomicFileCommitter()),
            new WorkspacePathComparison());
        await recoveryStore.WriteStatusAsync(new RecoveryStatus
        {
            CommitId = "commit-id",
            SolutionPath = fixture.ProjectPath,
            State = RecoveryState.RecoveryIncomplete,
            Message = "Message",
        }, TestContext.Current.CancellationToken);
        await using var target = WorkspaceCoordinatorFactory.Create(new WorkspaceRuntimeOptions
        {
            StateDirectory = stateDirectory.DirectoryPath,
        });

        var result = await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.RequiredAction.Should().Be(RequiredAction.ResolveRecovery);
    }
}
