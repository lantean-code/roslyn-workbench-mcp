using Microsoft.CodeAnalysis.Diagnostics;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetProjectDetailsToolTests
{
    [Fact]
    public async Task GIVEN_ResolveProjectHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new GetProjectDetailsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult.Rejected<ProjectDetailsData>(new PluginExecutionError
        {
            Code = "ProjectNotFound",
            Message = "ProjectNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProject<ProjectDetailsData>(
                It.IsAny<ProjectSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Rejected<Project, ProjectDetailsData>(expected));

        var result = await target.ExecuteAsync(new GetProjectDetailsRequest
        {
            Project = new ProjectSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_ProjectCompilationOptionsAreUnavailableAndIncludeDocumentsIsFalse_WHEN_CallingExecuteAsync_THEN_ShouldReturnProjectDetailsWithoutDocuments()
    {
        using var document = RoslynTestFactory.CreateUnsupportedDocument();

        var target = new GetProjectDetailsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var project = document.Solution.Projects.Single();
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProject<ProjectDetailsData>(
                It.IsAny<ProjectSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<Project, ProjectDetailsData>(project));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.NormalizeProjectPath(It.IsAny<string>()))
            .Returns<string>(item => item);

        queryContextMocks.ProjectTargetFrameworkResolver
            .Setup(item => item.Resolve("WorkspaceId", project, It.IsAny<CancellationToken>()))
            .Returns(ProjectTargetFrameworksResult.Succeeded(["TargetFramework"]));

        var result = await target.ExecuteAsync(new GetProjectDetailsRequest
        {
            Project = new ProjectSelector(),
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
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "C.cs",
                        Source = "public class ThirdDocument { }",
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
        var metadataReferenceImage = await File.ReadAllBytesAsync(typeof(object).Assembly.Location, TestContext.Current.CancellationToken);
        var metadataReferenceWithoutPath = MetadataReference.CreateFromImage(metadataReferenceImage);
        solution.Workspace.TryApplyChanges(
            solution.Solution
                .AddMetadataReference(mainProject.Id, metadataReferenceWithoutPath)
                .AddAnalyzerReference(mainProject.Id, analyzerReferenceOne.Object)
                .AddAnalyzerReference(mainProject.Id, analyzerReferenceTwo.Object)
                .AddAnalyzerReference(mainProject.Id, analyzerReferenceThree.Object));

        mainProject = solution.Workspace.CurrentSolution.Projects.Single(item => item.Name == "Main");

        var target = new GetProjectDetailsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
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
            .Returns(ToolResolutionResult.Resolved<Project, ProjectDetailsData>(mainProject));

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

        queryContextMocks.ProjectTargetFrameworkResolver
            .Setup(item => item.Resolve("WorkspaceId", mainProject, It.IsAny<CancellationToken>()))
            .Returns(ProjectTargetFrameworksResult.Succeeded(["net10.0", "net9.0"]));

        var result = await target.ExecuteAsync(new GetProjectDetailsRequest
        {
            Project = new ProjectSelector(),
            IncludeDocuments = true,
            DocumentsLimit = 1,
            ProjectReferencesLimit = 1,
            MetadataReferencesLimit = 10,
            AnalyzersLimit = 1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Project!.Name.Should().Be("Main");
        result.Data.Documents!.Items.Should().ContainSingle();
        result.Data.Documents.Items[0].Path.Should().Be("A.cs");
        result.Data.Documents.HasMore.Should().BeTrue();
        result.Data.Documents.TotalCount.Should().BeNull();
        result.Data.ProjectReferences.Items.Should().ContainSingle();
        result.Data.ProjectReferences.Items[0].Name.Should().Be("AnotherReferenced");
        result.Data.ProjectReferences.HasMore.Should().BeTrue();
        result.Data.ProjectReferences.TotalCount.Should().Be(2);
        result.Data.MetadataReferences.Items.Should().Contain(item => item.Path == null);
        result.Data.MetadataReferences.Items.Should().Contain(item => item.Path != null);
        result.Data.MetadataReferences.TotalCount.Should().Be(4);
        result.Data.Analyzers.Items.Should().ContainSingle();
        result.Data.Analyzers.Items[0].DisplayName.Should().Be("AAnalyzer");
        result.Data.Analyzers.HasMore.Should().BeTrue();
        result.Data.Analyzers.TotalCount.Should().Be(3);
        queryContextMocks.WorkspaceResolver.Verify(item => item.CreateDocumentReference(It.IsAny<Document>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GIVEN_DocumentCannotBeProjectedAndMetadataLimitIsZero_WHEN_CallingExecuteAsync_THEN_ShouldReturnEmptyBoundedCollections()
    {
        using var solution = RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "Project",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Project.cs",
                        Source = "public class ProjectType { }",
                    },
                ],
            },
        ]);

        var project = solution.Solution.Projects.Single();
        solution.Workspace.TryApplyChanges(
            solution.Solution.AddMetadataReference(
                project.Id,
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location)));

        project = solution.Workspace.CurrentSolution.Projects.Single();

        var target = new GetProjectDetailsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Workspace.CurrentSolution);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProject<ProjectDetailsData>(
                It.IsAny<ProjectSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<Project, ProjectDetailsData>(project));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.NormalizeDocumentPath(It.IsAny<string>()))
            .Returns<string>(item => item);

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.NormalizeProjectPath(It.IsAny<string>()))
            .Returns<string>(item => item);

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateDocumentReference(It.IsAny<Document>()))
            .Returns((DocumentReference?)null);

        queryContextMocks.ProjectTargetFrameworkResolver
            .Setup(item => item.Resolve("WorkspaceId", project, It.IsAny<CancellationToken>()))
            .Returns(ProjectTargetFrameworksResult.Succeeded([]));

        var result = await target.ExecuteAsync(new GetProjectDetailsRequest
        {
            Project = new ProjectSelector(),
            IncludeDocuments = true,
            MetadataReferencesLimit = 0,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Documents!.Items.Should().BeEmpty();
        result.Data.Documents.HasMore.Should().BeFalse();
        result.Data.MetadataReferences.Items.Should().BeEmpty();
        result.Data.MetadataReferences.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_TargetFrameworksCannotBeEvaluated_WHEN_CallingExecuteAsync_THEN_ShouldReturnRetryableRejection()
    {
        using var solution = RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "Project",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Project.cs",
                        Source = "public class ProjectType { }",
                    },
                ],
            },
        ]);

        var target = new GetProjectDetailsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var project = solution.Solution.Projects.Single();
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProject<ProjectDetailsData>(
                It.IsAny<ProjectSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<Project, ProjectDetailsData>(project));

        queryContextMocks.ProjectTargetFrameworkResolver
            .Setup(item => item.Resolve("WorkspaceId", project, It.IsAny<CancellationToken>()))
            .Returns(ProjectTargetFrameworksResult.Failed("Failure"));

        var result = await target.ExecuteAsync(
            new GetProjectDetailsRequest
            {
                Project = new ProjectSelector(),
            },
            queryContextMocks.QueryContext.Object,
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("ProjectStructureUnavailable");
        result.Error.Message.Should().Be("Failure");
        result.RequiredAction.Should().Be(RequiredAction.Retry);
    }
}
