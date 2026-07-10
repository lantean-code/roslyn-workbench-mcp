using Microsoft.CodeAnalysis.Diagnostics;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetProjectDetailsToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterQueryTool()
    {
        var registry = new Mock<IPluginRegistry>();

        GetProjectDetailsTool.Register(registry.Object);

        registry.Verify(item => item.RegisterQueryTool<GetProjectDetailsRequest, ProjectDetailsData>(
            It.Is<ToolRegistrationMetadata>(metadata =>
                metadata.Name == "get-project-details"
                && metadata.Title == "Get Project Details"
                && metadata.Description == "Returns project metadata, options and selected document details."),
            It.IsAny<IQueryToolHandler<GetProjectDetailsRequest, ProjectDetailsData>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ResolveProjectHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new GetProjectDetailsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<ProjectDetailsData>.Rejected(new PluginExecutionError
        {
            Code = "ProjectNotFound",
            Message = "ProjectNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProject<ProjectDetailsData>(
                It.IsAny<ProjectSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<Project, ProjectDetailsData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new GetProjectDetailsRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_ProjectCompilationIsNullAndIncludeDocumentsIsFalse_WHEN_CallingExecuteAsync_THEN_ShouldReturnProjectDetailsWithoutDocuments()
    {
        using var document = RoslynTestFactory.CreateUnsupportedDocument();

        var target = new GetProjectDetailsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var project = document.Solution.Projects.Single();
        var projectStructureService = new Mock<IProjectStructureService>();

        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.ProjectStructureService)
            .Returns(projectStructureService.Object);
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProject<ProjectDetailsData>(
                It.IsAny<ProjectSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<Project, ProjectDetailsData>
            {
                Value = project,
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.NormalizeProjectPath(It.IsAny<string>()))
            .Returns<string>(item => item);
        projectStructureService
            .Setup(item => item.GetTargetFrameworks(project))
            .Returns(["TargetFramework"]);

        var result = await target.ExecuteAsync(new GetProjectDetailsRequest
        {
            IncludeDocuments = false,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Documents.Should().BeNull();
        result.Data.Project!.Path.Should().Be(project.FilePath);
        result.Data.Project.TargetFrameworks.Should().Equal("TargetFramework");
        result.Data.CompilationOptions.Should().NotBeNull();
    }

    [Fact]
    public async Task GIVEN_ProjectIncludesDocumentsReferencesMetadataAndAnalyzers_WHEN_CallingExecuteAsync_THEN_ShouldReturnBoundedOrderedDetails()
    {
        using var solution = RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "AnotherReferenced",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "AnotherReferenced.cs",
                        Source = "public class AnotherReferencedType { }",
                    },
                ],
            },
            new InMemoryRoslynProjectDefinition
            {
                Name = "Referenced",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Referenced.cs",
                        Source = "public class ReferencedType { }",
                    },
                ],
            },
            new InMemoryRoslynProjectDefinition
            {
                Name = "Main",
                ProjectReferences = ["Referenced", "AnotherReferenced"],
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "B.cs",
                        Source = "public class SecondDocument { }",
                    },
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "A.cs",
                        Source = "public class FirstDocument { }",
                    },
                ],
            },
        ]);

        var analyzerReferenceOne = new Mock<AnalyzerReference>();
        var analyzerReferenceTwo = new Mock<AnalyzerReference>();
        analyzerReferenceOne
            .SetupGet(item => item.Display)
            .Returns("ZAnalyzer");
        analyzerReferenceOne
            .Setup(item => item.GetAnalyzers(It.IsAny<string>()))
            .Returns([]);
        analyzerReferenceOne
            .Setup(item => item.GetGenerators(It.IsAny<string>()))
            .Returns([]);
        analyzerReferenceTwo
            .SetupGet(item => item.Display)
            .Returns("AAnalyzer");
        analyzerReferenceTwo
            .Setup(item => item.GetAnalyzers(It.IsAny<string>()))
            .Returns([]);
        analyzerReferenceTwo
            .Setup(item => item.GetGenerators(It.IsAny<string>()))
            .Returns([]);
        var analyzerReferenceThree = new Mock<AnalyzerReference>();
        analyzerReferenceThree
            .SetupGet(item => item.Display)
            .Returns((string)null!);
        analyzerReferenceThree
            .Setup(item => item.GetAnalyzers(It.IsAny<string>()))
            .Returns([]);
        analyzerReferenceThree
            .Setup(item => item.GetGenerators(It.IsAny<string>()))
            .Returns([]);

        var mainProject = solution.Solution.Projects.Single(item => item.Name == "Main");
        var metadataReferenceWithoutPath = MetadataReference.CreateFromImage(File.ReadAllBytes(typeof(object).Assembly.Location));
        solution.Workspace.TryApplyChanges(
            solution.Solution
                .AddMetadataReference(mainProject.Id, metadataReferenceWithoutPath)
                .AddAnalyzerReference(mainProject.Id, analyzerReferenceOne.Object)
                .AddAnalyzerReference(mainProject.Id, analyzerReferenceTwo.Object)
                .AddAnalyzerReference(mainProject.Id, analyzerReferenceThree.Object));
        mainProject = solution.Workspace.CurrentSolution.Projects.Single(item => item.Name == "Main");

        var target = new GetProjectDetailsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var projectStructureService = new Mock<IProjectStructureService>();

        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.ProjectStructureService)
            .Returns(projectStructureService.Object);
        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Workspace.CurrentSolution);
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProject<ProjectDetailsData>(
                It.IsAny<ProjectSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<Project, ProjectDetailsData>
            {
                Value = mainProject,
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.NormalizeProjectPath(It.IsAny<string>()))
            .Returns<string>(item => Path.GetFileNameWithoutExtension(item));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.NormalizeDocumentPath(It.IsAny<string>()))
            .Returns<string>(item => Path.GetFileName(item));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateDocumentReference(It.IsAny<Document>()))
            .Returns<Document>(item => new DocumentReference
            {
                DocumentId = item.Id.Id.ToString(),
                ProjectId = item.Project.Id.Id.ToString(),
                Path = Path.GetFileName(item.FilePath)!,
            });
        projectStructureService
            .Setup(item => item.GetTargetFrameworks(mainProject))
            .Returns(["net10.0", "net9.0"]);

        var result = await target.ExecuteAsync(new GetProjectDetailsRequest
        {
            IncludeDocuments = true,
            DocumentsLimit = new CollectionLimit
            {
                MaxResults = 1,
            },
            ProjectReferencesLimit = new CollectionLimit
            {
                MaxResults = 1,
            },
            MetadataReferencesLimit = new CollectionLimit
            {
                MaxResults = 10,
            },
            AnalyzersLimit = new CollectionLimit
            {
                MaxResults = 1,
            },
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Project!.Name.Should().Be("Main");
        result.Data.Documents!.Items.Should().ContainSingle();
        result.Data.Documents.Items[0].Path.Should().Be("A.cs");
        result.Data.Documents.HasMore.Should().BeTrue();
        result.Data.ProjectReferences.Items.Should().ContainSingle();
        result.Data.ProjectReferences.Items[0].Name.Should().Be("AnotherReferenced");
        result.Data.MetadataReferences.Items.Should().Contain(item => item.Path == null);
        result.Data.MetadataReferences.Items.Should().Contain(item => item.Path != null);
        result.Data.Analyzers.Items.Should().ContainSingle();
        result.Data.Analyzers.Items[0].DisplayName.Should().Be("AAnalyzer");
        result.Data.Analyzers.HasMore.Should().BeTrue();
    }
}
