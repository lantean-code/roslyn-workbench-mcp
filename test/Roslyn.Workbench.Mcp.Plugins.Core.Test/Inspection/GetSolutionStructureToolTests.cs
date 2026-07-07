namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetSolutionStructureToolTests
{
    [Fact]
    public async Task GIVEN_ProjectStructureServiceHierarchy_WHEN_CallingExecute_THEN_ShouldUseConfiguredFoldersAndFrameworks()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class GreetingFormatter
            {
            }
            """);
        var workspaceIdentity = workspace.CreateWorkspaceIdentity();
        var resolver = workspace.CreateResolver(workspaceIdentity);
        var projectStructureService = new Mock<IProjectStructureService>();
        var services = new ToolExecutionServicesBuilder()
            .WithProjectStructureService(projectStructureService.Object)
            .Build();
        var context = new QueryContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithResolver(resolver)
            .WithWorkspaceIdentity(workspaceIdentity)
            .WithToolExecutionServices(services)
            .Build();
        var target = new GetSolutionStructureTool();
        var project = workspace.Solution.Projects.Single();

        projectStructureService
            .Setup(service => service.GetSolutionHierarchyAsync(
                workspaceIdentity.LoadedPath,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                Folders: (IReadOnlyList<SolutionFolderInfo>)
                [
                    new SolutionFolderInfo
                    {
                        Name = "core",
                        Path = "src/core",
                        ParentPath = "src",
                    },
                ],
                ProjectFolderPaths: (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [resolver.NormalizeProjectPath(project.FilePath!)] = "src/core",
                }));
        projectStructureService
            .Setup(service => service.GetTargetFrameworks(project))
            .Returns(["net10.0"]);

        var result = await target.ExecuteAsync(new GetSolutionStructureRequest(), context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Folders.Items.Should().ContainSingle(static folder => folder.Path == "src/core" && folder.ParentPath == "src");
        result.Data.Projects.Items.Should().ContainSingle(static item => item.TargetFrameworks.Count == 1 && item.TargetFrameworks[0] == "net10.0" && item.SolutionFolderPath == "src/core");
    }

    [Fact]
    public async Task GIVEN_ProjectWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnProjectsAndDocuments()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new GetSolutionStructureTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "get-solution-structure", target, new GetSolutionStructureRequest
        {
            IncludeDocuments = true,
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Projects.Items.Should().ContainSingle(static project => project.Name == "Sample" && project.Documents != null && project.Documents.Count > 0);
    }

    [Fact]
    public async Task GIVEN_SolutionHierarchyWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnFolders()
    {
        using var fixture = await SolutionHierarchyFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.SolutionPath,
        }, CancellationToken.None);
        var target = new GetSolutionStructureTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "get-solution-structure", target, new GetSolutionStructureRequest());

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Folders.Items.Should().Contain(static folder => folder.Path == "src");
        result.Data.Folders.Items.Should().Contain(static folder => folder.Path == "src/core" && folder.ParentPath == "src");
        result.Data.Projects.Items.Should().ContainSingle(static project => project.Name == "Lib" && project.SolutionFolderPath == "src/core");
    }
}
