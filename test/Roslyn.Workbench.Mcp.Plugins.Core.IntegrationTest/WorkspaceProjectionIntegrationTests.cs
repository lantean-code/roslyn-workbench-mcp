namespace Roslyn.Workbench.Mcp.Plugins.Core.Test;

public sealed class WorkspaceProjectionIntegrationTests
{
    [Fact]
    public async Task GIVEN_LoadedProject_WHEN_ProjectingWorkspaceDetails_THEN_ShouldIncludeDocumentsOptionsAndMetadataReferences()
    {
        using var fixture = InspectionSampleFixture.Create();
        await using var coordinator = BundledComponentWorkspaceFactory.CreateInspectionWorkspace();
        var openResult = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        var session = new PluginComponentTestSession(coordinator, BundledPluginCatalogueFactory.CreateCatalogue());

        var solution = await session.ExecuteQueryAsync<GetSolutionStructureRequest, SolutionStructureData>(
            "get-solution-structure",
            new GetSolutionStructureRequest(),
            TestContext.Current.CancellationToken);

        var project = await session.ExecuteQueryAsync<GetProjectDetailsRequest, ProjectDetailsData>(
            "get-project-details",
            new GetProjectDetailsRequest
            {
                Project = new ProjectSelector
                {
                    Path = "Sample.csproj",
                },
                IncludeDocuments = true,
            }, TestContext.Current.CancellationToken);

        var document = await session.ExecuteQueryAsync<GetDocumentOptionsRequest, DocumentOptionsData>(
            "get-document-options",
            new GetDocumentOptionsRequest
            {
                Document = new DocumentSelector
                {
                    Path = "Formatting.cs",
                },
            }, TestContext.Current.CancellationToken);

        openResult.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
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
        using var fixture = SolutionHierarchyFixture.Create();
        await using var coordinator = BundledComponentWorkspaceFactory.CreateInspectionWorkspace();
        var openResult = await coordinator.OpenAsync(fixture.SolutionPath, TestContext.Current.CancellationToken);
        var session = new PluginComponentTestSession(coordinator, BundledPluginCatalogueFactory.CreateCatalogue());

        var solution = await session.ExecuteQueryAsync<GetSolutionStructureRequest, SolutionStructureData>(
            "get-solution-structure",
            new GetSolutionStructureRequest(),
            TestContext.Current.CancellationToken);

        var application = await session.ExecuteQueryAsync<GetProjectDetailsRequest, ProjectDetailsData>(
            "get-project-details",
            new GetProjectDetailsRequest
            {
                Project = new ProjectSelector
                {
                    Path = "App/App.csproj",
                },
            }, TestContext.Current.CancellationToken);

        openResult.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        solution.Data!.Folders.Items.Should().Contain(static folder => folder.Path == "src/core" && folder.ParentPath == "src");
        solution.Data.Folders.Items.Should().Contain(static folder => folder.Path == "src/apps" && folder.ParentPath == "src");
        solution.Data.Projects.Items.Should().ContainSingle(static project => project.Name == "Lib" && project.SolutionFolderPath == "src/core");
        solution.Data.Projects.Items.Should().ContainSingle(static project => project.Name == "App" && project.SolutionFolderPath == "src/apps");
        application.Data!.ProjectReferences.Items.Should().ContainSingle(static reference => reference.Name == "Lib");
        application.Data.MetadataReferences.Items.Should().NotBeEmpty();
    }
}
