using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test;

public sealed class WorkspaceProjectionIntegrationTests
{
    [Fact]
    public async Task GIVEN_LoadedProject_WHEN_ProjectingWorkspaceDetails_THEN_ShouldIncludeDocumentsOptionsAndMetadataReferences()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var registry = BundledPluginRegistryFactory.CreateRegistry();

        var solution = await PluginToolTestHarness.InvokeAsync<SolutionStructureData>(coordinator, registry, "get-solution-structure", new Dictionary<string, JsonElement>());
        var project = await PluginToolTestHarness.InvokeAsync<ProjectDetailsData>(coordinator, registry, "get-project-details", new Dictionary<string, JsonElement>
        {
            ["project"] = JsonSerializer.SerializeToElement(new ProjectSelector
            {
                Path = "Sample.csproj",
            }),
            ["includeDocuments"] = JsonSerializer.SerializeToElement(true),
        });
        var document = await PluginToolTestHarness.InvokeAsync<DocumentOptionsData>(coordinator, registry, "get-document-options", new Dictionary<string, JsonElement>
        {
            ["document"] = JsonSerializer.SerializeToElement(new DocumentSelector
            {
                Path = "Formatting.cs",
            }),
        });

        openResult.Outcome.Should().Be(ToolOutcome.Succeeded);
        solution.Data!.Projects.Items.Should().ContainSingle(static item => item.Name == "Sample");
        project.Data!.Project!.Name.Should().Be("Sample");
        project.Data.Documents!.Items.Should().Contain(static item => item.Path == "Formatting.cs");
        project.Data.MetadataReferences.Items.Should().NotBeEmpty();
        project.Data.CompilationOptions.Should().NotBeNull();
        document.Data!.AnalyzerConfig!.EditorConfigPaths.Should().Contain(static path => path.EndsWith(".editorconfig", StringComparison.Ordinal));
        document.Data.AnalyzerConfig.Options.Should().ContainKey("build_property.targetframework");
    }

    [Fact]
    public async Task GIVEN_MultiProjectSolution_WHEN_ProjectingWorkspace_THEN_ShouldIncludeFoldersAndProjectReferences()
    {
        using var fixture = await SolutionHierarchyFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.SolutionPath,
        }, CancellationToken.None);
        var registry = BundledPluginRegistryFactory.CreateRegistry();

        var solution = await PluginToolTestHarness.InvokeAsync<SolutionStructureData>(coordinator, registry, "get-solution-structure", new Dictionary<string, JsonElement>());
        var application = await PluginToolTestHarness.InvokeAsync<ProjectDetailsData>(coordinator, registry, "get-project-details", new Dictionary<string, JsonElement>
        {
            ["project"] = JsonSerializer.SerializeToElement(new ProjectSelector
            {
                Path = "App/App.csproj",
            }),
        });

        openResult.Outcome.Should().Be(ToolOutcome.Succeeded);
        solution.Data!.Folders.Items.Should().Contain(static folder => folder.Path == "src/core" && folder.ParentPath == "src");
        solution.Data.Folders.Items.Should().Contain(static folder => folder.Path == "src/apps" && folder.ParentPath == "src");
        solution.Data.Projects.Items.Should().ContainSingle(static project => project.Name == "Lib" && project.SolutionFolderPath == "src/core");
        solution.Data.Projects.Items.Should().ContainSingle(static project => project.Name == "App" && project.SolutionFolderPath == "src/apps");
        application.Data!.ProjectReferences.Items.Should().ContainSingle(static reference => reference.Name == "Lib");
        application.Data.MetadataReferences.Items.Should().NotBeEmpty();
    }
}
