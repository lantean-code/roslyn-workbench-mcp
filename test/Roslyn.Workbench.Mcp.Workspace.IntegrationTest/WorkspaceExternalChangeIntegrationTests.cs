namespace Roslyn.Workbench.Mcp.Workspace.Test;

public sealed class WorkspaceExternalChangeIntegrationTests
{
    [Fact]
    public async Task GIVEN_ChangedWorkspaceInput_WHEN_GettingStatus_THEN_ShouldTransitionToWorkspaceOutOfDate()
    {
        await using var fixture = await TestWorkspaceFixture.CreateAsync();
        await using var target = fixture.CreateWorkspace();
        await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await File.AppendAllTextAsync(fixture.DocumentPath, Environment.NewLine + "class Added { }", TestContext.Current.CancellationToken);

        var result = await target.GetStatusAsync(TestContext.Current.CancellationToken);

        result.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        result.Data!.State.Should().Be(WorkspaceLifecycleState.WorkspaceOutOfDate);
        result.Data.ReloadRequired.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_AddedWorkspaceInput_WHEN_GettingStatus_THEN_ShouldTransitionToWorkspaceOutOfDate()
    {
        await using var fixture = await TestWorkspaceFixture.CreateAsync();
        await using var target = fixture.CreateWorkspace();
        await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);

        var addedDocumentPath = Path.Combine(Path.GetDirectoryName(fixture.DocumentPath)!, "Added.cs");
        await File.WriteAllTextAsync(addedDocumentPath, """
            namespace Sample;

            public sealed class Added
            {
            }
            """, TestContext.Current.CancellationToken);

        var result = await target.GetStatusAsync(TestContext.Current.CancellationToken);

        result.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        result.Data!.State.Should().Be(WorkspaceLifecycleState.WorkspaceOutOfDate);
        result.Data.ReloadRequired.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_ChangedDirectoryBuildProps_WHEN_GettingStatus_THEN_ShouldTransitionToWorkspaceOutOfDate()
    {
        await using var fixture = await TestWorkspaceFixture.CreateAsync();
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
        await using var fixture = await TestWorkspaceFixture.CreateAsync();
        await using var target = fixture.CreateWorkspace();
        await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await File.AppendAllTextAsync(fixture.EditorConfigPath, Environment.NewLine + "dotnet_diagnostic.CS0168.severity = warning", TestContext.Current.CancellationToken);

        await using var result = target.CreateQueryContext(new QueryRequest(), TestContext.Current.CancellationToken);

        result.ShortCircuitResult.Should().NotBeNull();
        result.ShortCircuitResult!.Error!.Code.Should().Be("WorkspaceOutOfDate");
        result.ShortCircuitResult.RequiredAction.Should().Be(RequiredAction.ReloadWorkspace);
    }

    [Fact]
    public async Task GIVEN_OutOfDateWorkspace_WHEN_Reloading_THEN_ShouldTransitionBackToReady()
    {
        await using var fixture = await TestWorkspaceFixture.CreateAsync();
        await using var target = fixture.CreateWorkspace();
        await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await File.AppendAllTextAsync(fixture.DocumentPath, Environment.NewLine + "class Added { }", TestContext.Current.CancellationToken);
        await target.GetStatusAsync(TestContext.Current.CancellationToken);

        var result = await target.ReloadAsync(TestContext.Current.CancellationToken);

        result.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        result.Data!.Workspace.Should().NotBeNull();

        var status = await target.GetStatusAsync(TestContext.Current.CancellationToken);

        status.Data!.State.Should().Be(WorkspaceLifecycleState.Ready);
        status.Data.ReloadRequired.Should().BeFalse();
    }


    [Fact]
    public async Task GIVEN_MalformedProjectAfterExternalChange_WHEN_ReloadingWorkspace_THEN_ShouldReturnStructuredLoadDiagnostics()
    {
        await using var fixture = await TestWorkspaceFixture.CreateAsync();
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

    private sealed record QueryRequest : WorkspaceBoundRequest;
}
