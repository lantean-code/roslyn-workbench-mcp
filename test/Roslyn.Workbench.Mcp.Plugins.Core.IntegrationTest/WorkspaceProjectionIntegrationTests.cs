using Microsoft.CodeAnalysis;
using Roslyn.Workbench.Mcp.Workspace.Loading;

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
            new GetSolutionStructureRequest
            {
                IncludeDocuments = true,
                DocumentsPerProjectLimit = 100,
            },
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
                DocumentsLimit = 100,
            }, TestContext.Current.CancellationToken);

        var document = await session.ExecuteQueryAsync<GetDocumentOptionsRequest, DocumentOptionsData>(
            "get-document-options",
            new GetDocumentOptionsRequest
            {
                Document = new DocumentSelector
                {
                    Path = "Formatting.cs",
                },
                IncludeParseOptions = true,
                IncludeAnalyzerConfig = true,
            }, TestContext.Current.CancellationToken);

        openResult.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        var workspaceId = openResult.Context.WorkspaceId
            ?? throw new InvalidOperationException("The open operation did not return a workspace identifier.");
        coordinator.GetCurrentSolution(workspaceId).Projects
            .SelectMany(static candidate => candidate.Documents)
            .Should().Contain(static candidate => HasIntermediatePathSegment(candidate.FilePath));
        solution.Data!.Projects.Items.Should().ContainSingle(static item => item.Name == "Sample");
        solution.Data.Projects.Items.Single().Documents!.Items.Should().OnlyContain(static item =>
            !item.Path.Split('/').Contains("obj", StringComparer.OrdinalIgnoreCase));
        project.Data!.Project!.Name.Should().Be("Sample");
        project.Data.Documents!.Items.Should().Contain(static item => item.Path == "Formatting.cs");
        project.Data.Documents.Items.Should().OnlyContain(static item =>
            !item.Path.Split('/').Contains("obj", StringComparer.OrdinalIgnoreCase));
        project.Data.MetadataReferences.Items.Should().NotBeEmpty();
        project.Data.CompilationOptions.Should().NotBeNull();
        document.Data!.AnalyzerConfig!.EditorConfigPaths.Should().Contain(static path => path.EndsWith(".editorconfig", StringComparison.Ordinal));
        document.Data.ParseOptions!.Language.Should().Be(LanguageNames.CSharp);
        document.Data.AnalyzerConfig.Options.Should().ContainKey("build_property.targetframework");
    }

    private static bool HasIntermediatePathSegment(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .Contains("obj", StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GIVEN_LoadedProjectHasDefinedConstants_WHEN_ProjectingProjectDetails_THEN_ShouldIncludeEffectivePreprocessorSymbols()
    {
        using var fixture = InspectionSampleFixture.Create(InspectionSampleProfile.PreprocessorSymbols);
        await using var coordinator = BundledComponentWorkspaceFactory.CreateInspectionWorkspace();
        var openResult = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        var session = new PluginComponentTestSession(coordinator, BundledPluginCatalogueFactory.CreateCatalogue());

        var project = await session.ExecuteQueryAsync<GetProjectDetailsRequest, ProjectDetailsData>(
            "get-project-details",
            new GetProjectDetailsRequest
            {
                Project = new ProjectSelector
                {
                    Path = "Sample.csproj",
                },
            },
            TestContext.Current.CancellationToken);

        openResult.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        project.Data!.CompilationOptions!.PreprocessorSymbols.Should().Contain("PROJECT_DETAILS_ALPHA");
        project.Data.CompilationOptions.PreprocessorSymbols.Should().Contain("PROJECT_DETAILS_ZETA");
        project.Data.CompilationOptions.PreprocessorSymbols.Should().Contain("NET10_0");
        project.Data.CompilationOptions.PreprocessorSymbols.Should().BeInAscendingOrder(StringComparer.Ordinal);
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

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_WorkspaceHasConfigurationProperty_WHEN_ProjectingWorkspace_THEN_ShouldReportEvaluatedTargetFramework()
    {
        using var fixture = InspectionSampleFixture.Create();
        await File.WriteAllTextAsync(
            fixture.ProjectPath,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework Condition="'$(Configuration)' == 'Release'">net9.0</TargetFramework>
                <TargetFramework Condition="'$(Configuration)' != 'Release'">net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """,
            TestContext.Current.CancellationToken);

        var properties = new WorkspaceMsBuildProperties
        {
            Configuration = "Release",
        };

        await using var coordinator = BundledComponentWorkspaceFactory.CreateInspectionWorkspace();
        var openResult = await coordinator.OpenAsync(
            fixture.ProjectPath,
            TestContext.Current.CancellationToken,
            msBuildProperties: properties);

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
            },
            TestContext.Current.CancellationToken);

        openResult.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        solution.Data!.Projects.Items.Should().ContainSingle().Which.TargetFrameworks.Should().Equal("net9.0");
        project.Data!.Project!.TargetFrameworks.Should().Equal("net9.0");
    }
}
