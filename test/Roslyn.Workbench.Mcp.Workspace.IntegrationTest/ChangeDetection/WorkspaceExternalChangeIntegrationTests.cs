using Roslyn.Workbench.Mcp.Workspace.Lifecycle;

namespace Roslyn.Workbench.Mcp.Workspace.Test.ChangeDetection;

public sealed class WorkspaceExternalChangeIntegrationTests
{
    [Fact]
    public async Task GIVEN_ChangedWorkspaceInput_WHEN_GettingStatus_THEN_ShouldTransitionToWorkspaceOutOfDate()
    {
        using var fixture = TestWorkspaceFixture.Create();
        await using var target = fixture.CreateWorkspace();
        await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await using var observer = fixture.CreateWorkspace();
        await observer.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await File.AppendAllTextAsync(fixture.DocumentPath, Environment.NewLine + "class Added { }", TestContext.Current.CancellationToken);

        var result = await target.GetStatusAsync(TestContext.Current.CancellationToken);
        var observedState = await ObserveOtherInstanceStateAsync(
            observer,
            WorkspaceLifecycleState.WorkspaceOutOfDate);

        result.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        result.Data!.State.Should().Be(WorkspaceLifecycleState.WorkspaceOutOfDate);
        result.Data.ReloadRequired.Should().BeTrue();
        observedState.Should().Be(WorkspaceLifecycleState.WorkspaceOutOfDate);
    }

    [Fact]
    public async Task GIVEN_AddedWorkspaceInput_WHEN_GettingStatus_THEN_ShouldTransitionToWorkspaceOutOfDate()
    {
        using var fixture = TestWorkspaceFixture.Create();
        await using var target = fixture.CreateWorkspace();
        await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);

        var addedDocumentPath = Path.Combine(Path.GetDirectoryName(fixture.DocumentPath)!, "Added.cs");
        await File.WriteAllTextAsync(addedDocumentPath, """
            namespace Sample;

            public sealed class Added
            {
            }
            """, TestContext.Current.CancellationToken);

        // New inputs arrive through the asynchronous watcher rather than existing-file metadata polling.
        var result = await WaitForOutOfDateStatusAsync(target);

        result.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        result.Data!.State.Should().Be(WorkspaceLifecycleState.WorkspaceOutOfDate);
        result.Data.ReloadRequired.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_ChangedDirectoryBuildProps_WHEN_GettingStatus_THEN_ShouldTransitionToWorkspaceOutOfDate()
    {
        using var fixture = TestWorkspaceFixture.Create();
        await using var target = fixture.CreateWorkspace();
        await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await File.AppendAllTextAsync(fixture.DirectoryBuildPropsPath, Environment.NewLine + "<!-- changed -->", TestContext.Current.CancellationToken);

        var result = await target.GetStatusAsync(TestContext.Current.CancellationToken);

        result.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        result.Data!.State.Should().Be(WorkspaceLifecycleState.WorkspaceOutOfDate);
        result.Data.ReloadRequired.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_ChangedEditorConfig_WHEN_CreatingQueryContext_THEN_ShouldRejectAsWorkspaceOutOfDate()
    {
        using var fixture = TestWorkspaceFixture.Create();
        await using var target = fixture.CreateWorkspace();
        await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await File.AppendAllTextAsync(fixture.EditorConfigPath, Environment.NewLine + "dotnet_diagnostic.CS0168.severity = warning", TestContext.Current.CancellationToken);

        await using var result = target.CreateQueryContext(new QueryRequest(), TestContext.Current.CancellationToken);

        result.ShortCircuitResult.Should().NotBeNull();
        result.ShortCircuitResult!.Error!.Code.Should().Be("WorkspaceOutOfDate");
        result.ShortCircuitResult.RequiredAction.Should().Be(RequiredAction.ReloadWorkspace);
    }

    [Fact]
    public async Task GIVEN_OutOfDateWorkspace_WHEN_Reloading_THEN_ShouldTransitionAndRepublishReady()
    {
        using var fixture = TestWorkspaceFixture.Create();
        await using var target = fixture.CreateWorkspace();
        await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await using var observer = fixture.CreateWorkspace();
        await observer.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await File.AppendAllTextAsync(fixture.DocumentPath, Environment.NewLine + "class Added { }", TestContext.Current.CancellationToken);
        await target.GetStatusAsync(TestContext.Current.CancellationToken);
        var observedOutOfDateState = await ObserveOtherInstanceStateAsync(
            observer,
            WorkspaceLifecycleState.WorkspaceOutOfDate);

        var result = await target.ReloadAsync(TestContext.Current.CancellationToken);
        var observedReadyState = await ObserveOtherInstanceStateAsync(
            observer,
            WorkspaceLifecycleState.Ready);

        result.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        result.Data!.Workspace.Should().NotBeNull();
        observedOutOfDateState.Should().Be(WorkspaceLifecycleState.WorkspaceOutOfDate);
        observedReadyState.Should().Be(WorkspaceLifecycleState.Ready);

        var status = await target.GetStatusAsync(TestContext.Current.CancellationToken);

        status.Data!.State.Should().Be(WorkspaceLifecycleState.Ready);
        status.Data.ReloadRequired.Should().BeFalse();
    }

    [Fact]
    public async Task GIVEN_MalformedProjectAfterExternalChange_WHEN_ReloadingWorkspace_THEN_ShouldReturnStructuredLoadDiagnostics()
    {
        using var fixture = TestWorkspaceFixture.Create();
        await using var target = fixture.CreateWorkspace();
        await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(fixture.ProjectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
            """, TestContext.Current.CancellationToken);

        await target.GetStatusAsync(TestContext.Current.CancellationToken);

        var result = await target.ReloadAsync(TestContext.Current.CancellationToken);

        result.Status.Should().Be(WorkspaceOperationStatus.Rejected);
        result.Error!.Code.Should().Be("WorkspaceLoadFailed");
        result.Diagnostics.Should().NotBeEmpty();
    }

    private static async ValueTask<WorkspaceOperationResult<WorkspaceStatusOutcome>> WaitForOutOfDateStatusAsync(ComponentWorkspace workspace)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(10));
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(20));

        try
        {
            while (true)
            {
                var result = await workspace.GetStatusAsync(timeoutSource.Token);
                if (result.Status != WorkspaceOperationStatus.Succeeded
                    || result.Data?.State != WorkspaceLifecycleState.Ready)
                {
                    return result;
                }

                await timer.WaitForNextTickAsync(timeoutSource.Token);
            }
        }
        catch (OperationCanceledException exception) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The workspace remained Ready for 10 seconds after adding a source file.", exception);
        }
    }

    private static async ValueTask<WorkspaceLifecycleState?> ObserveOtherInstanceStateAsync(
        ComponentWorkspace observer,
        WorkspaceLifecycleState expectedState)
    {
        WorkspaceLifecycleState? observedState = null;
        for (var attempt = 0; attempt < 1000 && observedState != expectedState; attempt++)
        {
            var result = await observer.GetStatusAsync(TestContext.Current.CancellationToken);
            observedState = result.Data?.Instances
                .Select(static instance => (WorkspaceLifecycleState?)instance.WorkspaceState)
                .FirstOrDefault(state => state == expectedState);

            await Task.Yield();
        }

        return observedState;
    }

    private sealed record QueryRequest : WorkspaceBoundRequest;
}
