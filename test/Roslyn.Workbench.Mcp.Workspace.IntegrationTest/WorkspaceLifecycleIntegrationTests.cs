namespace Roslyn.Workbench.Mcp.Workspace.Test;

public sealed class WorkspaceLifecycleIntegrationTests
{
    [Fact]
    public async Task GIVEN_UnloadedCoordinator_WHEN_OpeningWorkspace_THEN_ShouldTransitionToReady()
    {
        using var fixture = TestWorkspaceFixture.Create();
        await using var target = fixture.CreateWorkspace();

        var result = await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);

        result.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        result.Data.Should().NotBeNull();
        result.Data!.Workspace.Should().NotBeNull();
        result.Data.ProjectCount.Should().Be(1);
        result.Data.DocumentCount.Should().BeGreaterThan(0);

        var status = await target.GetStatusAsync(TestContext.Current.CancellationToken);

        status.Data!.State.Should().Be(WorkspaceLifecycleState.Ready);
        status.Data.Workspace!.LoadedPath.Should().Be(fixture.ProjectPath);
        status.Context.WorkspaceEpoch.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GIVEN_ReadyCoordinator_WHEN_ClosingWorkspace_THEN_ShouldTransitionToUnloaded()
    {
        using var fixture = TestWorkspaceFixture.Create();
        await using var target = fixture.CreateWorkspace();
        await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);

        var result = await target.CloseAsync(TestContext.Current.CancellationToken);

        result.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        result.Data!.ClosedPath.Should().Be(fixture.ProjectPath);

        var status = await target.GetStatusAsync(TestContext.Current.CancellationToken);

        status.Status.Should().Be(WorkspaceOperationStatus.Rejected);
        status.Error!.Code.Should().Be("WorkspaceNotOpen");
    }

    [Fact]
    public async Task GIVEN_AnotherLiveServerInstance_WHEN_OpeningAndQueryingStatus_THEN_ShouldSurfaceItsAdvisoryState()
    {
        using var fixture = TestWorkspaceFixture.Create();
        using var firstStateDirectory = TemporaryDirectory.Create("roslyn-workbench-mcp-state-tests");
        using var secondStateDirectory = TemporaryDirectory.Create("roslyn-workbench-mcp-state-tests");
        await using var first = ComponentWorkspace.Create(new ComponentWorkspaceOptions
        {
            StateDirectory = firstStateDirectory.DirectoryPath,
        });
        await using var second = ComponentWorkspace.Create(new ComponentWorkspaceOptions
        {
            StateDirectory = secondStateDirectory.DirectoryPath,
        });
        await first.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);

        var secondOpen = await second.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        var status = await second.GetStatusAsync(TestContext.Current.CancellationToken);

        secondOpen.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        secondOpen.Data!.LoadDiagnostics.Should().Contain(diagnostic => diagnostic.Id == "WorkspaceInUse");
        status.Data!.Instances.Should().ContainSingle();
        status.Data.Instances[0].LoadedPath.Should().Be(fixture.ProjectPath);
        status.Data.Instances[0].WorkspaceState.Should().Be(WorkspaceLifecycleState.Ready);

        await second.CloseAsync(TestContext.Current.CancellationToken);
        await first.CloseAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GIVEN_IndependentDefaultRuntimes_WHEN_CreatedAndDisposed_THEN_ShouldUseAndDeleteUniqueStateDirectories()
    {
        string firstStateDirectory;
        string secondStateDirectory;
        await using (var first = ComponentWorkspace.Create())
        {
            await using (var second = ComponentWorkspace.Create())
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
        using var fixtureA = TestWorkspaceFixture.Create();
        using var fixtureB = TestWorkspaceFixture.Create();
        await using var target = fixtureA.CreateWorkspace();

        var openA = await target.OpenAsync(
            fixtureA.ProjectPath,
            TestContext.Current.CancellationToken,
            alias: "alpha");
        var openB = await target.OpenAsync(
            fixtureB.ProjectPath,
            TestContext.Current.CancellationToken,
            alias: "beta");

        var list = await target.ListAsync(TestContext.Current.CancellationToken);
        var ambiguousStatus = await target.GetStatusAsync(TestContext.Current.CancellationToken);
        var selectedStatus = await target.GetStatusAsync(
            TestContext.Current.CancellationToken,
            workspaceId: openB.Data!.Workspace.WorkspaceId);

        list.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        list.Data!.Workspaces.Should().HaveCount(2);
        list.Data.Workspaces.Select(static workspace => workspace.WorkspaceId).Should().Contain([openA.Data!.Workspace.WorkspaceId, openB.Data!.Workspace.WorkspaceId]);
        ambiguousStatus.Status.Should().Be(WorkspaceOperationStatus.Rejected);
        ambiguousStatus.Error!.Code.Should().Be("WorkspaceSelectorRequired");
        selectedStatus.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        selectedStatus.Data!.Workspace!.WorkspaceId.Should().Be(openB.Data!.Workspace!.WorkspaceId);
        selectedStatus.Data.Workspace.Alias.Should().Be("beta");
    }

    [Fact]
    public async Task GIVEN_NonSdkStyleProject_WHEN_OpeningWorkspace_THEN_ShouldRejectRequest()
    {
        using var fixture = TestWorkspaceFixture.CreateLegacyProject();
        await using var target = fixture.CreateWorkspace();

        var result = await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);

        result.Status.Should().Be(WorkspaceOperationStatus.Rejected);
        result.Error!.Code.Should().Be("WorkspaceNotSupported");
    }

    [Fact]
    public async Task GIVEN_MalformedProject_WHEN_OpeningWorkspace_THEN_ShouldReturnStructuredLoadDiagnostics()
    {
        using var fixture = TestWorkspaceFixture.CreateMalformedProject();
        await using var target = fixture.CreateWorkspace();

        var result = await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);

        result.Status.Should().Be(WorkspaceOperationStatus.Rejected);
        result.Error!.Code.Should().Be("WorkspaceLoadFailed");
        result.Diagnostics.Should().NotBeEmpty();
    }


    [Fact]
    public async Task GIVEN_UnresolvedRecoveryState_WHEN_OpeningWorkspace_THEN_ShouldRejectRequest()
    {
        using var fixture = TestWorkspaceFixture.Create();
        using var stateDirectory = TemporaryDirectory.Create("roslyn-workbench-mcp-recovery-tests");
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
        await using var target = ComponentWorkspace.Create(new ComponentWorkspaceOptions
        {
            StateDirectory = stateDirectory.DirectoryPath,
        });

        var result = await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);

        result.Status.Should().Be(WorkspaceOperationStatus.Rejected);
        result.Error!.RequiredAction.Should().Be(RequiredAction.ResolveRecovery);
    }
}
