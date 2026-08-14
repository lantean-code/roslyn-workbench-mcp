namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetSolutionStructureToolTests
{
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
        var referencedProject = solution.Solution.Projects.Single(item => item.Name == "Referenced");
        var mainProject = solution.Solution.Projects.Single(item => item.Name == "Main");
        var currentSolution = solution.Solution.AddProjectReference(
            mainProject.Id,
            new ProjectReference(ProjectId.CreateNewId()));

        var target = new GetSolutionStructureTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var projectStructureService = new Mock<IProjectStructureService>();

        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.ProjectStructureService)
            .Returns(projectStructureService.Object);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(currentSolution);

        queryContextMocks.QueryContext
            .SetupGet(item => item.WorkspaceIdentity)
            .Returns(new WorkspaceIdentity
            {
                WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                LoadedPath = "/workspace/Sample.slnx",
            });

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        string? referencedProjectPath = "Referenced";
        string? mainProjectPath = "Main";
        queryContextMocks.WorkspacePathService
            .Setup(item => item.TryNormalizePath(referencedProject.FilePath!, out referencedProjectPath))
            .Returns(true);

        queryContextMocks.WorkspacePathService
            .Setup(item => item.TryNormalizePath(mainProject.FilePath!, out mainProjectPath))
            .Returns(true);

        projectStructureService
            .Setup(item => item.GetSolutionHierarchyAsync(
                It.Is<WorkspaceIdentity>(workspace => workspace.LoadedPath == "/workspace/Sample.slnx"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SolutionHierarchyResult.Succeeded(
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

        var targetFrameworksByProjectName = new Dictionary<string, ProjectTargetFrameworksResult>(StringComparer.Ordinal)
        {
            ["Main"] = ProjectTargetFrameworksResult.Succeeded(["net10.0"]),
            ["Referenced"] = ProjectTargetFrameworksResult.Succeeded(["net9.0"]),
        };

        queryContextMocks.ProjectTargetFrameworkResolver
            .Setup(item => item.Resolve(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                It.IsAny<IReadOnlyList<Project>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Guid _, IReadOnlyList<Project> projects, CancellationToken _) => projects
                .Select(project => targetFrameworksByProjectName[project.Name])
                .ToArray());

        var result = await target.ExecuteAsync(new GetSolutionStructureRequest
        {
            IncludeDocuments = false,
            FoldersLimit = 1,
            ProjectsLimit = 1,
            ProjectReferencesPerProjectLimit = 0,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.SolutionPath.Should().Be("/workspace/Sample.slnx");
        result.Data.Folders.Items.Should().ContainSingle();
        result.Data.Folders.HasMore.Should().BeTrue();
        result.Data.Projects.Items.Should().ContainSingle();
        result.Data.Projects.Items[0].Name.Should().Be("Main");
        result.Data.Projects.Items[0].SolutionFolderPath.Should().Be("/src/core");
        result.Data.Projects.Items[0].Documents.Should().BeNull();
        result.Data.Projects.Items[0].ProjectReferences.Items.Should().BeEmpty();
        result.Data.Projects.Items[0].ProjectReferences.HasMore.Should().BeTrue();
        result.Data.Projects.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_DocumentsExceedPerProjectLimit_WHEN_CallingExecuteAsync_THEN_ShouldReturnOrderedBoundedDocumentReferences()
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
        var mainProject = solution.Solution.Projects.Single();

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
                WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                LoadedPath = "/workspace/Sample.slnx",
            });

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        string? mainProjectPath = "Main";
        string? firstDocumentPath = "A.cs";
        string? secondDocumentPath = "B.cs";
        queryContextMocks.WorkspacePathService
            .Setup(item => item.TryNormalizePath(mainProject.FilePath!, out mainProjectPath))
            .Returns(true);

        queryContextMocks.WorkspacePathService
            .Setup(item => item.TryNormalizePath(It.Is<string>(path => path.EndsWith("A.cs", StringComparison.Ordinal)), out firstDocumentPath))
            .Returns(true);

        queryContextMocks.WorkspacePathService
            .Setup(item => item.TryNormalizePath(It.Is<string>(path => path.EndsWith("B.cs", StringComparison.Ordinal)), out secondDocumentPath))
            .Returns(true);

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateDocumentReference(It.IsAny<Document>()))
            .Returns<Document>(item => new DocumentReference
            {
                DocumentId = item.Id.Id.ToString(),
                ProjectId = item.Project.Id.Id.ToString(),
                Path = Path.GetFileName(item.FilePath)!,
            });

        projectStructureService
            .Setup(item => item.GetSolutionHierarchyAsync(
                It.Is<WorkspaceIdentity>(workspace => workspace.LoadedPath == "/workspace/Sample.slnx"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SolutionHierarchyResult.Succeeded());

        queryContextMocks.ProjectTargetFrameworkResolver
            .Setup(item => item.Resolve(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                It.IsAny<IReadOnlyList<Project>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Guid _, IReadOnlyList<Project> projects, CancellationToken _) => projects
                .Select(static _ => ProjectTargetFrameworksResult.Succeeded())
                .ToArray());

        var result = await target.ExecuteAsync(new GetSolutionStructureRequest
        {
            IncludeDocuments = true,
            DocumentsPerProjectLimit = 1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Projects.Items.Should().ContainSingle();
        result.Data.Projects.Items[0].TargetFrameworks.Should().BeEmpty();
        result.Data.Projects.Items[0].Documents.Should().NotBeNull();
        result.Data.Projects.Items[0].Documents!.Items.Select(item => item.Path).Should().Equal("A.cs");
        result.Data.Projects.Items[0].Documents!.HasMore.Should().BeTrue();

        var zeroLimitResult = await target.ExecuteAsync(new GetSolutionStructureRequest
        {
            IncludeDocuments = true,
            DocumentsPerProjectLimit = 0,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        zeroLimitResult.Data!.Projects.Items[0].Documents!.Items.Should().BeEmpty();
        zeroLimitResult.Data.Projects.Items[0].Documents!.HasMore.Should().BeTrue();
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
                WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                LoadedPath = "/workspace/Sample.slnx",
            });

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        string? referencedAPath = "B-ReferencedA";
        string? referencedBPath = "A-ReferencedB";
        string? mainProjectPath = "Main";
        string? mainDocumentPath = "Main.cs";
        queryContextMocks.WorkspacePathService
            .Setup(item => item.TryNormalizePath("ReferencedA", out referencedAPath))
            .Returns(true);

        queryContextMocks.WorkspacePathService
            .Setup(item => item.TryNormalizePath(referencedB.FilePath!, out referencedBPath))
            .Returns(true);

        queryContextMocks.WorkspacePathService
            .Setup(item => item.TryNormalizePath("Main", out mainProjectPath))
            .Returns(true);

        queryContextMocks.WorkspacePathService
            .Setup(item => item.TryNormalizePath(It.Is<string>(path => path.EndsWith("Main.cs", StringComparison.Ordinal)), out mainDocumentPath))
            .Returns(true);

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateDocumentReference(It.IsAny<Document>()))
            .Returns<Document>(item => new DocumentReference
            {
                DocumentId = item.Id.Id.ToString(),
                ProjectId = item.Project.Id.Id.ToString(),
                Path = Path.GetFileName(item.FilePath)!,
            });

        projectStructureService
            .Setup(item => item.GetSolutionHierarchyAsync(
                It.Is<WorkspaceIdentity>(workspace => workspace.LoadedPath == "/workspace/Sample.slnx"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SolutionHierarchyResult.Succeeded(
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

        var targetFrameworksByProject = new Dictionary<Project, ProjectTargetFrameworksResult>
        {
            [referencedA] = ProjectTargetFrameworksResult.Succeeded(["net8.0"]),
            [referencedB] = ProjectTargetFrameworksResult.Succeeded(["net9.0"]),
            [mainProject] = ProjectTargetFrameworksResult.Succeeded(["net10.0"]),
        };

        queryContextMocks.ProjectTargetFrameworkResolver
            .Setup(item => item.Resolve(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                It.IsAny<IReadOnlyList<Project>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Guid _, IReadOnlyList<Project> projects, CancellationToken _) => projects
                .Select(project => targetFrameworksByProject[project])
                .ToArray());

        var result = await target.ExecuteAsync(new GetSolutionStructureRequest
        {
            IncludeDocuments = true,
            ProjectReferencesPerProjectLimit = 1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        var mainProjectResult = result.Data!.Projects.Items.Single(item => item.Name == "Main");

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        mainProjectResult.Path.Should().Be("Main");
        mainProjectResult.SolutionFolderPath.Should().Be("/src/core");
        mainProjectResult.ProjectReferences.Items.Select(item => item.Name).Should().Equal("ReferencedB");
        mainProjectResult.ProjectReferences.HasMore.Should().BeTrue();
        mainProjectResult.ProjectReferences.TotalCount.Should().Be(2);
        mainProjectResult.Documents!.Items.Select(item => item.Path).Should().ContainSingle().Which.Should().Be("Main.cs");
    }

    [Fact]
    public async Task GIVEN_SolutionHierarchyCannotBeLoaded_WHEN_CallingExecuteAsync_THEN_ShouldReturnRetryableRejection()
    {
        var target = new GetSolutionStructureTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var projectStructureService = new Mock<IProjectStructureService>();
        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.ProjectStructureService)
            .Returns(projectStructureService.Object);

        queryContextMocks.QueryContext
            .SetupGet(item => item.WorkspaceIdentity)
            .Returns(new WorkspaceIdentity
            {
                WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                LoadedPath = "/workspace/Sample.slnx",
            });

        projectStructureService
            .Setup(item => item.GetSolutionHierarchyAsync(
                It.Is<WorkspaceIdentity>(workspace => workspace.LoadedPath == "/workspace/Sample.slnx"),
                TestContext.Current.CancellationToken))
            .ReturnsAsync(SolutionHierarchyResult.Failed("Failure"));

        var result = await target.ExecuteAsync(
            new GetSolutionStructureRequest(),
            queryContextMocks.QueryContext.Object,
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("ProjectStructureUnavailable");
        result.Error.Message.Should().Be("Failure");
        result.RequiredAction.Should().Be(RequiredAction.Retry);
        queryContextMocks.ProjectTargetFrameworkResolver.Verify(item => item.Resolve(
            It.IsAny<Guid>(),
            It.IsAny<IReadOnlyList<Project>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_LoadedProjectPathCannotBeNormalized_WHEN_CallingExecuteAsync_THEN_ShouldReturnReloadRejection()
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
                        Name = "Main.cs",
                        Source = "public class MainType { }",
                    },
                ],
            },
        ]);

        var target = new GetSolutionStructureTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var projectStructureService = new Mock<IProjectStructureService>();
        var mainProject = solution.Solution.Projects.Single();
        string? normalizedPath = null;
        queryContextMocks.QueryContext.SetupGet(item => item.CurrentSolution).Returns(solution.Solution);
        queryContextMocks.QueryContext.SetupGet(item => item.WorkspaceIdentity).Returns(new WorkspaceIdentity());
        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.ProjectStructureService)
            .Returns(projectStructureService.Object);

        projectStructureService
            .Setup(item => item.GetSolutionHierarchyAsync(It.IsAny<WorkspaceIdentity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SolutionHierarchyResult.Succeeded());

        queryContextMocks.WorkspacePathService
            .Setup(item => item.TryNormalizePath(mainProject.FilePath!, out normalizedPath))
            .Returns(false);

        var result = await target.ExecuteAsync(
            new GetSolutionStructureRequest(),
            queryContextMocks.QueryContext.Object,
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("ProjectStructureUnavailable");
        result.RequiredAction.Should().Be(RequiredAction.ReloadWorkspace);
        queryContextMocks.ProjectTargetFrameworkResolver.Verify(item => item.Resolve(
            It.IsAny<Guid>(),
            It.IsAny<IReadOnlyList<Project>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ProjectFrameworksCannotBeEvaluated_WHEN_CallingExecuteAsync_THEN_ShouldReturnRetryableRejection()
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
                WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                LoadedPath = "/workspace/Sample.slnx",
            });

        var mainProject = solution.Solution.Projects.Single();
        string? mainProjectPath = "Main";
        queryContextMocks.WorkspacePathService
            .Setup(item => item.TryNormalizePath(mainProject.FilePath!, out mainProjectPath))
            .Returns(true);

        projectStructureService
            .Setup(item => item.GetSolutionHierarchyAsync(
                It.Is<WorkspaceIdentity>(workspace => workspace.LoadedPath == "/workspace/Sample.slnx"),
                TestContext.Current.CancellationToken))
            .ReturnsAsync(SolutionHierarchyResult.Succeeded());

        queryContextMocks.ProjectTargetFrameworkResolver
            .Setup(item => item.Resolve(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                It.IsAny<IReadOnlyList<Project>>(),
                It.IsAny<CancellationToken>()))
            .Returns([ProjectTargetFrameworksResult.Failed("Failure")]);

        var result = await target.ExecuteAsync(
            new GetSolutionStructureRequest(),
            queryContextMocks.QueryContext.Object,
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("ProjectStructureUnavailable");
        result.Error.Message.Should().Be("Failure");
        result.RequiredAction.Should().Be(RequiredAction.Retry);
    }
}
