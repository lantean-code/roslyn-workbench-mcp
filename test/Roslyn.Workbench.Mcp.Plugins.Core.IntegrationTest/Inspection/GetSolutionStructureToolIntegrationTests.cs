namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

[Trait("Category", "Integration")]
public sealed class GetSolutionStructureToolIntegrationTests
{
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
