namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetSolutionStructureToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterQueryTool()
    {
        var registry = new Mock<IPluginRegistry>();

        GetSolutionStructureTool.Register(registry.Object);

        registry.Verify(item => item.RegisterQueryTool<GetSolutionStructureRequest, SolutionStructureData>(
            It.Is<ToolRegistrationMetadata>(metadata =>
                metadata.Name == "get-solution-structure"
                && metadata.Title == "Get Solution Structure"
                && metadata.Description == "Returns solution folders, projects, target frameworks and direct project relationships."),
            It.IsAny<IQueryToolHandler<GetSolutionStructureRequest, SolutionStructureData>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_IncludeDocumentsIsFalse_WHEN_CallingExecuteAsync_THEN_ShouldReturnBoundedSolutionStructureWithoutDocuments()
    {
        using var solution = RoslynTestFactory.CreateSolution(
        [
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
                ProjectReferences = ["Referenced"],
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Main.cs",
                        Source = "public class MainType { }",
                    },
                ],
            },
        ]);

        var target = new GetSolutionStructureTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var projectStructureService = new Mock<IProjectStructureService>();

        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.ProjectStructureService)
            .Returns(projectStructureService.Object);
        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);
        queryContextMocks.QueryContext
            .SetupGet(item => item.WorkspaceIdentity)
            .Returns(new WorkspaceIdentity
            {
                WorkspaceId = "WorkspaceId",
                LoadedPath = "/workspace/Sample.slnx",
            });
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.NormalizeProjectPath(It.IsAny<string>()))
            .Returns<string>(item => Path.GetFileNameWithoutExtension(item));
        projectStructureService
            .Setup(item => item.GetSolutionHierarchyAsync("/workspace/Sample.slnx", It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                (IReadOnlyList<SolutionFolderInfo>)
                [
                    new SolutionFolderInfo
                    {
                        Path = "/src/core",
                    },
                    new SolutionFolderInfo
                    {
                        Path = "/src",
                    },
                ],
                (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["Main"] = "/src/core",
                }));
        projectStructureService
            .Setup(item => item.GetTargetFrameworks(It.Is<Project>(project => project.Name == "Main")))
            .Returns(["net10.0"]);
        projectStructureService
            .Setup(item => item.GetTargetFrameworks(It.Is<Project>(project => project.Name == "Referenced")))
            .Returns(["net9.0"]);

        var result = await target.ExecuteAsync(new GetSolutionStructureRequest
        {
            IncludeDocuments = false,
            FoldersLimit = new CollectionLimit
            {
                MaxResults = 1,
            },
            ProjectsLimit = new CollectionLimit
            {
                MaxResults = 1,
            },
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.SolutionPath.Should().Be("/workspace/Sample.slnx");
        result.Data.Folders.Items.Should().ContainSingle();
        result.Data.Folders.HasMore.Should().BeTrue();
        result.Data.Projects.Items.Should().ContainSingle();
        result.Data.Projects.Items[0].Name.Should().Be("Main");
        result.Data.Projects.Items[0].SolutionFolderPath.Should().Be("/src/core");
        result.Data.Projects.Items[0].Documents.Should().BeNull();
        result.Data.Projects.Items[0].ProjectReferences.Should().ContainSingle(item => item.Name == "Referenced");
        result.Data.Projects.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_IncludeDocumentsIsTrue_WHEN_CallingExecuteAsync_THEN_ShouldReturnOrderedDocumentReferences()
    {
        using var solution = RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "Main",
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

        var target = new GetSolutionStructureTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var projectStructureService = new Mock<IProjectStructureService>();

        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.ProjectStructureService)
            .Returns(projectStructureService.Object);
        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);
        queryContextMocks.QueryContext
            .SetupGet(item => item.WorkspaceIdentity)
            .Returns(new WorkspaceIdentity
            {
                WorkspaceId = "WorkspaceId",
                LoadedPath = "/workspace/Sample.slnx",
            });
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
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
            .Setup(item => item.GetSolutionHierarchyAsync("/workspace/Sample.slnx", It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                (IReadOnlyList<SolutionFolderInfo>)[],
                (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>(StringComparer.Ordinal)));
        projectStructureService
            .Setup(item => item.GetTargetFrameworks(It.IsAny<Project>()))
            .Returns(["net10.0"]);

        var result = await target.ExecuteAsync(new GetSolutionStructureRequest
        {
            IncludeDocuments = true,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Projects.Items.Should().ContainSingle();
        result.Data.Projects.Items[0].Documents.Should().NotBeNull();
        result.Data.Projects.Items[0].Documents!.Select(item => item.Path).Should().Equal("A.cs", "B.cs");
    }

    [Fact]
    public async Task GIVEN_ProjectFilePathIsNullAndMultipleProjectReferencesExist_WHEN_CallingExecuteAsync_THEN_ShouldUseNameFallbacksAndOrderProjectReferences()
    {
        using var solution = RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "ReferencedB",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "ReferencedB.cs",
                        Source = "public class ReferencedBType { }",
                    },
                ],
            },
            new InMemoryRoslynProjectDefinition
            {
                Name = "ReferencedA",
                UseDefaultFilePathWhenNull = false,
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "ReferencedA.cs",
                        Source = "public class ReferencedAType { }",
                    },
                ],
            },
            new InMemoryRoslynProjectDefinition
            {
                Name = "Main",
                UseDefaultFilePathWhenNull = false,
                ProjectReferences = ["ReferencedB", "ReferencedA"],
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Main.cs",
                        Source = "public class MainType { }",
                    },
                ],
            },
        ]);

        var referencedA = solution.Solution.Projects.Single(item => item.Name == "ReferencedA");
        var referencedB = solution.Solution.Projects.Single(item => item.Name == "ReferencedB");
        var mainProject = solution.Solution.Projects.Single(item => item.Name == "Main");
        var updatedSolution = solution.Solution;

        var target = new GetSolutionStructureTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var projectStructureService = new Mock<IProjectStructureService>();

        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.ProjectStructureService)
            .Returns(projectStructureService.Object);
        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(updatedSolution);
        queryContextMocks.QueryContext
            .SetupGet(item => item.WorkspaceIdentity)
            .Returns(new WorkspaceIdentity
            {
                WorkspaceId = "WorkspaceId",
                LoadedPath = "/workspace/Sample.slnx",
            });
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.NormalizeProjectPath(It.IsAny<string>()))
            .Returns<string>(item => item);
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
            .Setup(item => item.GetSolutionHierarchyAsync("/workspace/Sample.slnx", It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                (IReadOnlyList<SolutionFolderInfo>)
                [
                    new SolutionFolderInfo
                    {
                        Path = "/src/core",
                    },
                ],
                (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["Main"] = "/src/core",
                }));
        projectStructureService
            .Setup(item => item.GetTargetFrameworks(referencedA))
            .Returns(["net8.0"]);
        projectStructureService
            .Setup(item => item.GetTargetFrameworks(referencedB))
            .Returns(["net9.0"]);
        projectStructureService
            .Setup(item => item.GetTargetFrameworks(mainProject))
            .Returns(["net10.0"]);

        var result = await target.ExecuteAsync(new GetSolutionStructureRequest
        {
            IncludeDocuments = true,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        var mainProjectResult = result.Data!.Projects.Items.Single(item => item.Name == "Main");

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        mainProjectResult.Path.Should().Be("Main");
        mainProjectResult.SolutionFolderPath.Should().Be("/src/core");
        mainProjectResult.ProjectReferences.Select(item => item.Name).Should().Equal("ReferencedB", "ReferencedA");
        mainProjectResult.Documents!.Select(item => item.Path).Should().ContainSingle().Which.Should().Be("Main.cs");
    }
}
