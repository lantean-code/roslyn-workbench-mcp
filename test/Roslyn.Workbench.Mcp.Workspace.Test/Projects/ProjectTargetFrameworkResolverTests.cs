using Roslyn.Workbench.Mcp.Workspace.Caching;
using Roslyn.Workbench.Mcp.Workspace.Projects.Caching;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Projects;

public sealed class ProjectTargetFrameworkResolverTests
{
    [Fact]
    public void GIVEN_CachedProject_WHEN_GettingTargetFrameworks_THEN_ShouldReturnCachedResult()
    {
        using var solution = CreateSolution();
        var project = solution.Solution.Projects.Single();
        var projectStructureService = new Mock<IProjectStructureService>();
        var cacheScopeFactory = new Mock<IWorkspaceQueryCacheScopeFactory>();
        var cacheScope = new Mock<IWorkspaceQueryCacheScope>();
        string[] cachedTargetFrameworks = ["net10.0"];
        var cacheEntry = new ProjectTargetFrameworkCacheEntry(cachedTargetFrameworks);

        cacheScopeFactory
            .Setup(item => item.CreateScope("WorkspaceId", project.Solution, "project-target-framework"))
            .Returns(cacheScope.Object);

        cacheScope
            .Setup(item => item.GetOrCreateProjected<
                ProjectTargetFrameworkCacheKey,
                ProjectTargetFrameworkCacheEntry,
                ProjectTargetFrameworksResult>(
                It.IsAny<ProjectTargetFrameworkCacheKey>(),
                It.IsAny<Func<CancellationToken, ProjectTargetFrameworksResult>>(),
                It.IsAny<Func<ProjectTargetFrameworksResult, ProjectTargetFrameworkCacheEntry?>>(),
                It.IsAny<Func<ProjectTargetFrameworkCacheEntry, ProjectTargetFrameworksResult>>(),
                It.IsAny<Func<ProjectTargetFrameworkCacheEntry, long>>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                ProjectTargetFrameworkCacheKey _,
                Func<CancellationToken, ProjectTargetFrameworksResult> _,
                Func<ProjectTargetFrameworksResult, ProjectTargetFrameworkCacheEntry?> _,
                Func<ProjectTargetFrameworkCacheEntry, ProjectTargetFrameworksResult> cachedResultSelector,
                Func<ProjectTargetFrameworkCacheEntry, long> _,
                CancellationToken _) => cachedResultSelector(cacheEntry));

        var target = new ProjectTargetFrameworkResolver(
            projectStructureService.Object,
            cacheScopeFactory.Object);

        var result = target.Resolve(
            "WorkspaceId",
            project,
            TestContext.Current.CancellationToken);

        result.TargetFrameworks.Should().Equal("net10.0");
        projectStructureService.Verify(
            item => item.GetTargetFrameworks(It.IsAny<Project>()),
            Times.Never);
    }

    [Fact]
    public void GIVEN_ProjectWithoutPath_WHEN_GettingTargetFrameworks_THEN_ShouldReturnEmptyWithoutCacheOrEvaluation()
    {
        using var solution = RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "Project",
                UseDefaultFilePathWhenNull = false,
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Project.cs",
                        Source = "class Project { }",
                    },
                ],
            },
        ]);

        var project = solution.Solution.Projects.Single();
        var projectStructureService = new Mock<IProjectStructureService>();
        var cacheScopeFactory = new Mock<IWorkspaceQueryCacheScopeFactory>();
        var target = new ProjectTargetFrameworkResolver(
            projectStructureService.Object,
            cacheScopeFactory.Object);

        var result = target.Resolve(
            "WorkspaceId",
            project,
            TestContext.Current.CancellationToken);

        result.TargetFrameworks.Should().BeEmpty();
        cacheScopeFactory.VerifyNoOtherCalls();
        projectStructureService.VerifyNoOtherCalls();
    }

    [Fact]
    public void GIVEN_FailedEvaluation_WHEN_GettingTargetFrameworks_THEN_ShouldReturnSharedResultWithoutCacheEntry()
    {
        using var solution = CreateSolution();
        var project = solution.Solution.Projects.Single();
        var projectStructureService = new Mock<IProjectStructureService>();
        var cacheScopeFactory = new Mock<IWorkspaceQueryCacheScopeFactory>();
        var cacheScope = new Mock<IWorkspaceQueryCacheScope>();
        var failedResult = ProjectTargetFrameworksResult.Failed("Failure");

        projectStructureService
            .Setup(item => item.GetTargetFrameworks(project))
            .Returns(failedResult);

        cacheScopeFactory
            .Setup(item => item.CreateScope("WorkspaceId", project.Solution, "project-target-framework"))
            .Returns(cacheScope.Object);

        cacheScope
            .Setup(item => item.GetOrCreateProjected<
                ProjectTargetFrameworkCacheKey,
                ProjectTargetFrameworkCacheEntry,
                ProjectTargetFrameworksResult>(
                It.IsAny<ProjectTargetFrameworkCacheKey>(),
                It.IsAny<Func<CancellationToken, ProjectTargetFrameworksResult>>(),
                It.IsAny<Func<ProjectTargetFrameworksResult, ProjectTargetFrameworkCacheEntry?>>(),
                It.IsAny<Func<ProjectTargetFrameworkCacheEntry, ProjectTargetFrameworksResult>>(),
                It.IsAny<Func<ProjectTargetFrameworkCacheEntry, long>>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                ProjectTargetFrameworkCacheKey _,
                Func<CancellationToken, ProjectTargetFrameworksResult> resultFactory,
                Func<ProjectTargetFrameworksResult, ProjectTargetFrameworkCacheEntry?> cacheValueSelector,
                Func<ProjectTargetFrameworkCacheEntry, ProjectTargetFrameworksResult> _,
                Func<ProjectTargetFrameworkCacheEntry, long> _,
                CancellationToken cancellationToken) =>
            {
                var result = resultFactory(cancellationToken);
                cacheValueSelector(result).Should().BeNull();
                return result;
            });

        var target = new ProjectTargetFrameworkResolver(
            projectStructureService.Object,
            cacheScopeFactory.Object);

        var result = target.Resolve(
            "WorkspaceId",
            project,
            TestContext.Current.CancellationToken);

        result.IsSucceeded.Should().BeFalse();
        result.ErrorMessage.Should().Be("Failure");
        projectStructureService.Verify(
            item => item.GetTargetFrameworks(project),
            Times.Once);
    }

    [Fact]
    public void GIVEN_CachedAndUncachedProjects_WHEN_GettingTargetFrameworks_THEN_ShouldBatchOnlyMissesAndPreserveOrder()
    {
        using var solution = CreateSolution("Cached", "FirstMiss", "SecondMiss");
        var projects = solution.Solution.Projects.ToArray();
        var cachedProject = projects.Single(item => item.Name == "Cached");
        var firstMiss = projects.Single(item => item.Name == "FirstMiss");
        var secondMiss = projects.Single(item => item.Name == "SecondMiss");
        var projectStructureService = new Mock<IProjectStructureService>();
        var cacheScopeFactory = new Mock<IWorkspaceQueryCacheScopeFactory>();
        var cacheScope = new Mock<IWorkspaceQueryCacheScope>();
        string[] cachedTargetFrameworks = ["net8.0"];
        var cachedEntry = new ProjectTargetFrameworkCacheEntry(cachedTargetFrameworks);
        ProjectTargetFrameworkCacheEntry? configuredCachedEntry = cachedEntry;
        string[] firstTargetFrameworks = ["net9.0"];
        string[] secondTargetFrameworks = ["net10.0"];
        var firstEvaluatedResult = ProjectTargetFrameworksResult.Succeeded(firstTargetFrameworks);
        var secondEvaluatedResult = ProjectTargetFrameworksResult.Succeeded(secondTargetFrameworks);
        ProjectTargetFrameworksResult[] evaluatedResults =
        [
            firstEvaluatedResult,
            secondEvaluatedResult,
        ];

        cacheScopeFactory
            .Setup(item => item.CreateScope(
                "WorkspaceId",
                It.IsAny<Solution>(),
                "project-target-framework"))
            .Returns(cacheScope.Object);

        cacheScope
            .Setup(item => item.TryGet(
                It.Is<ProjectTargetFrameworkCacheKey>(key =>
                    key.ProjectPath == cachedProject.FilePath),
                out configuredCachedEntry))
            .Returns(true);

        ProjectTargetFrameworkCacheEntry? missingEntry = null;
        cacheScope
            .Setup(item => item.TryGet(
                It.Is<ProjectTargetFrameworkCacheKey>(key =>
                    key.ProjectPath == firstMiss.FilePath
                    || key.ProjectPath == secondMiss.FilePath),
                out missingEntry))
            .Returns(false);

        projectStructureService
            .Setup(item => item.GetTargetFrameworks(
                It.Is<IReadOnlyList<Project>>(items =>
                    items.Count == 2
                    && items[0] == firstMiss
                    && items[1] == secondMiss)))
            .Returns(evaluatedResults);

        var target = new ProjectTargetFrameworkResolver(
            projectStructureService.Object,
            cacheScopeFactory.Object);

        var results = target.Resolve(
            "WorkspaceId",
            projects,
            TestContext.Current.CancellationToken);

        results[0].TargetFrameworks.Should().Equal("net8.0");
        results[1].TargetFrameworks.Should().Equal("net9.0");
        results[2].TargetFrameworks.Should().Equal("net10.0");
        projectStructureService.Verify(
            item => item.GetTargetFrameworks(It.IsAny<Project>()),
            Times.Never);

        cacheScope.Verify(item => item.Store(
            It.IsAny<ProjectTargetFrameworkCacheKey>(),
            It.IsAny<ProjectTargetFrameworkCacheEntry>(),
            It.IsAny<Func<ProjectTargetFrameworkCacheEntry, long>>()),
            Times.Exactly(2));
    }

    [Fact]
    public void GIVEN_BatchContainsFailure_WHEN_GettingTargetFrameworks_THEN_ShouldStoreOnlySuccessfulResults()
    {
        using var solution = CreateSolution("First", "Second");
        var projects = solution.Solution.Projects.ToArray();
        var projectStructureService = new Mock<IProjectStructureService>();
        var cacheScopeFactory = new Mock<IWorkspaceQueryCacheScopeFactory>();
        var cacheScope = new Mock<IWorkspaceQueryCacheScope>();
        ProjectTargetFrameworkCacheEntry? missingEntry = null;
        string[] successfulTargetFrameworks = ["net10.0"];
        var successfulResult = ProjectTargetFrameworksResult.Succeeded(successfulTargetFrameworks);
        var failedResult = ProjectTargetFrameworksResult.Failed("Failure");
        ProjectTargetFrameworksResult[] evaluatedResults =
        [
            successfulResult,
            failedResult,
        ];

        cacheScopeFactory
            .Setup(item => item.CreateScope(
                "WorkspaceId",
                It.IsAny<Solution>(),
                "project-target-framework"))
            .Returns(cacheScope.Object);

        cacheScope
            .Setup(item => item.TryGet(
                It.IsAny<ProjectTargetFrameworkCacheKey>(),
                out missingEntry))
            .Returns(false);

        projectStructureService
            .Setup(item => item.GetTargetFrameworks(It.IsAny<IReadOnlyList<Project>>()))
            .Returns(evaluatedResults);

        var target = new ProjectTargetFrameworkResolver(
            projectStructureService.Object,
            cacheScopeFactory.Object);

        var results = target.Resolve(
            "WorkspaceId",
            projects,
            TestContext.Current.CancellationToken);

        results[0].IsSucceeded.Should().BeTrue();
        results[1].IsSucceeded.Should().BeFalse();
        cacheScope.Verify(item => item.Store(
            It.IsAny<ProjectTargetFrameworkCacheKey>(),
            It.IsAny<ProjectTargetFrameworkCacheEntry>(),
            It.IsAny<Func<ProjectTargetFrameworkCacheEntry, long>>()),
            Times.Once);
    }

    [Fact]
    public void GIVEN_AllProjectsCached_WHEN_GettingTargetFrameworks_THEN_ShouldNotEvaluateBatch()
    {
        using var solution = CreateSolution();
        var project = solution.Solution.Projects.Single();
        var projectStructureService = new Mock<IProjectStructureService>();
        var cacheScopeFactory = new Mock<IWorkspaceQueryCacheScopeFactory>();
        var cacheScope = new Mock<IWorkspaceQueryCacheScope>();
        string[] cachedTargetFrameworks = ["net10.0"];
        var expectedEntry = new ProjectTargetFrameworkCacheEntry(cachedTargetFrameworks);
        ProjectTargetFrameworkCacheEntry? configuredEntry = expectedEntry;

        cacheScopeFactory
            .Setup(item => item.CreateScope("WorkspaceId", project.Solution, "project-target-framework"))
            .Returns(cacheScope.Object);

        cacheScope
            .Setup(item => item.TryGet(
                It.IsAny<ProjectTargetFrameworkCacheKey>(),
                out configuredEntry))
            .Returns(true);

        var target = new ProjectTargetFrameworkResolver(
            projectStructureService.Object,
            cacheScopeFactory.Object);

        var results = target.Resolve(
            "WorkspaceId",
            [project],
            TestContext.Current.CancellationToken);

        results.Should().ContainSingle()
            .Which.TargetFrameworks.Should().Equal("net10.0");
        projectStructureService.Verify(
            item => item.GetTargetFrameworks(It.IsAny<IReadOnlyList<Project>>()),
            Times.Never);
    }

    [Fact]
    public void GIVEN_PathlessProjectInBatch_WHEN_GettingTargetFrameworks_THEN_ShouldReturnEmptyWithoutEvaluation()
    {
        using var solution = RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "Project",
                UseDefaultFilePathWhenNull = false,
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Project.cs",
                        Source = "class Project { }",
                    },
                ],
            },
        ]);

        var project = solution.Solution.Projects.Single();
        var projectStructureService = new Mock<IProjectStructureService>();
        var cacheScopeFactory = new Mock<IWorkspaceQueryCacheScopeFactory>();
        var target = new ProjectTargetFrameworkResolver(
            projectStructureService.Object,
            cacheScopeFactory.Object);

        var results = target.Resolve(
            "WorkspaceId",
            [project],
            TestContext.Current.CancellationToken);

        results.Should().ContainSingle()
            .Which.TargetFrameworks.Should().BeEmpty();
        cacheScopeFactory.VerifyNoOtherCalls();
        projectStructureService.VerifyNoOtherCalls();
    }

    private static InMemoryRoslynSolution CreateSolution(params string[] projectNames)
    {
        if (projectNames.Length == 0)
        {
            projectNames = ["Project"];
        }

        var projects = new InMemoryRoslynProjectDefinition[projectNames.Length];
        for (var index = 0; index < projectNames.Length; index++)
        {
            projects[index] = new InMemoryRoslynProjectDefinition
            {
                Name = projectNames[index],
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = $"{projectNames[index]}.cs",
                        Source = $"class {projectNames[index]} {{ }}",
                    },
                ],
            };
        }

        return RoslynTestFactory.CreateSolution(projects);
    }
}
