using Roslyn.Workbench.Mcp.Plugins.Core.Projects.Caching;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Caching;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Projects;

public sealed class ProjectTargetFrameworkResolverTests
{
    [Fact]
    public void GIVEN_CachedProject_WHEN_GettingTargetFrameworks_THEN_ShouldReturnCachedResult()
    {
        using var solution = CreateSolution();
        var project = solution.Solution.Projects.Single();
        var projectStructureService = new Mock<IProjectStructureService>();
        var queryCache = new Mock<IQueryCache>();
        var target = new ProjectTargetFrameworkResolver(projectStructureService.Object, queryCache.Object);
        var cacheEntry = new ProjectTargetFrameworkCacheEntry(ProjectTargetFrameworksResult.Succeeded(["net10.0"]));
        var cacheKey = new ProjectTargetFrameworkCacheKey(project.Solution, project.FilePath!);

        queryCache
            .Setup(item => item.TryGet<ProjectTargetFrameworkCacheEntry>("WorkspaceId", cacheKey, out cacheEntry))
            .Returns(true);

        var result = target.Resolve(
            "WorkspaceId",
            project,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(cacheEntry.Result);
        projectStructureService.Verify(item => item.GetTargetFrameworks(It.IsAny<Project>()), Times.Never);
    }

    [Fact]
    public void GIVEN_UncachedProject_WHEN_EvaluationSucceeds_THEN_ShouldStoreSuccessfulResult()
    {
        using var solution = CreateSolution();
        var project = solution.Solution.Projects.Single();
        var projectStructureService = new Mock<IProjectStructureService>();
        var queryCache = new Mock<IQueryCache>();
        var target = new ProjectTargetFrameworkResolver(projectStructureService.Object, queryCache.Object);

        projectStructureService
            .Setup(item => item.GetTargetFrameworks(project))
            .Returns(ProjectTargetFrameworksResult.Succeeded(["net10.0"]));

        var result = target.Resolve(
            "WorkspaceId",
            project,
            TestContext.Current.CancellationToken);

        result.TargetFrameworks.Should().Equal("net10.0");
        queryCache.Verify(item => item.Store(
            "WorkspaceId",
            It.Is<ProjectTargetFrameworkCacheKey>(key => key.Equals(new ProjectTargetFrameworkCacheKey(project.Solution, project.FilePath!))),
            It.Is<ProjectTargetFrameworkCacheEntry>(entry =>
                entry.Result.TargetFrameworks.Count == 1
                && entry.Result.TargetFrameworks[0] == "net10.0"),
            2), Times.Once);
    }

    [Fact]
    public void GIVEN_UncachedProject_WHEN_EvaluationFails_THEN_ShouldNotStoreResult()
    {
        using var solution = CreateSolution();
        var project = solution.Solution.Projects.Single();
        var projectStructureService = new Mock<IProjectStructureService>();
        var queryCache = new Mock<IQueryCache>();
        var target = new ProjectTargetFrameworkResolver(projectStructureService.Object, queryCache.Object);

        projectStructureService
            .Setup(item => item.GetTargetFrameworks(project))
            .Returns(ProjectTargetFrameworksResult.Failed("Failure"));

        var result = target.Resolve(
            "WorkspaceId",
            project,
            TestContext.Current.CancellationToken);

        result.IsSucceeded.Should().BeFalse();
        queryCache.Verify(item => item.Store(
            It.IsAny<string>(),
            It.IsAny<object>(),
            It.IsAny<ProjectTargetFrameworkCacheEntry>(),
            It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public void GIVEN_ProjectWithoutPath_WHEN_GettingTargetFrameworks_THEN_ShouldReturnEmptyResultWithoutEvaluation()
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
        var queryCache = new Mock<IQueryCache>();
        var target = new ProjectTargetFrameworkResolver(projectStructureService.Object, queryCache.Object);

        var result = target.Resolve(
            "WorkspaceId",
            project,
            TestContext.Current.CancellationToken);

        result.TargetFrameworks.Should().BeEmpty();
        projectStructureService.Verify(item => item.GetTargetFrameworks(It.IsAny<Project>()), Times.Never);
    }

    [Fact]
    public void GIVEN_CachedAndUncachedProjects_WHEN_GettingTargetFrameworks_THEN_ShouldEvaluateOnlyMissesAndPreserveOrder()
    {
        using var solution = CreateSolution("Cached", "Uncached");
        var cachedProject = solution.Solution.Projects.Single(item => item.Name == "Cached");
        var uncachedProject = solution.Solution.Projects.Single(item => item.Name == "Uncached");
        var projectStructureService = new Mock<IProjectStructureService>();
        var queryCache = new Mock<IQueryCache>();
        var target = new ProjectTargetFrameworkResolver(projectStructureService.Object, queryCache.Object);
        var cacheEntry = new ProjectTargetFrameworkCacheEntry(ProjectTargetFrameworksResult.Succeeded(["net9.0"]));
        var cacheKey = new ProjectTargetFrameworkCacheKey(cachedProject.Solution, cachedProject.FilePath!);

        queryCache
            .Setup(item => item.TryGet<ProjectTargetFrameworkCacheEntry>("WorkspaceId", cacheKey, out cacheEntry))
            .Returns(true);
        projectStructureService
            .Setup(item => item.GetTargetFrameworks(It.Is<IReadOnlyList<Project>>(projects =>
                projects.Count == 1 && projects[0] == uncachedProject)))
            .Returns([ProjectTargetFrameworksResult.Succeeded(["net10.0"])]);

        var results = target.Resolve(
            "WorkspaceId",
            [cachedProject, uncachedProject],
            TestContext.Current.CancellationToken);

        results[0].TargetFrameworks.Should().Equal("net9.0");
        results[1].TargetFrameworks.Should().Equal("net10.0");
        queryCache.Verify(item => item.Store(
            "WorkspaceId",
            It.IsAny<ProjectTargetFrameworkCacheKey>(),
            It.IsAny<ProjectTargetFrameworkCacheEntry>(),
            2), Times.Once);
    }

    [Fact]
    public void GIVEN_AllProjectsCached_WHEN_GettingTargetFrameworks_THEN_ShouldNotEvaluateProjects()
    {
        using var solution = CreateSolution();
        var project = solution.Solution.Projects.Single();
        var projectStructureService = new Mock<IProjectStructureService>();
        var queryCache = new Mock<IQueryCache>();
        var target = new ProjectTargetFrameworkResolver(projectStructureService.Object, queryCache.Object);
        var cacheEntry = new ProjectTargetFrameworkCacheEntry(ProjectTargetFrameworksResult.Succeeded(["net10.0"]));
        var cacheKey = new ProjectTargetFrameworkCacheKey(project.Solution, project.FilePath!);

        queryCache
            .Setup(item => item.TryGet<ProjectTargetFrameworkCacheEntry>("WorkspaceId", cacheKey, out cacheEntry))
            .Returns(true);

        var results = target.Resolve(
            "WorkspaceId",
            [project],
            TestContext.Current.CancellationToken);

        results.Should().ContainSingle().Which.Should().BeSameAs(cacheEntry.Result);
        projectStructureService.Verify(item => item.GetTargetFrameworks(It.IsAny<IReadOnlyList<Project>>()), Times.Never);
    }

    [Fact]
    public void GIVEN_ProjectBatchContainsFailure_WHEN_GettingTargetFrameworks_THEN_ShouldNotStoreAnyMisses()
    {
        using var solution = CreateSolution("First", "Second");
        var projects = solution.Solution.Projects.ToArray();
        var projectStructureService = new Mock<IProjectStructureService>();
        var queryCache = new Mock<IQueryCache>();
        var target = new ProjectTargetFrameworkResolver(projectStructureService.Object, queryCache.Object);

        projectStructureService
            .Setup(item => item.GetTargetFrameworks(It.IsAny<IReadOnlyList<Project>>()))
            .Returns(
            [
                ProjectTargetFrameworksResult.Succeeded(["net10.0"]),
                ProjectTargetFrameworksResult.Failed("Failure"),
            ]);

        var results = target.Resolve(
            "WorkspaceId",
            projects,
            TestContext.Current.CancellationToken);

        results[0].IsSucceeded.Should().BeTrue();
        results[1].IsSucceeded.Should().BeFalse();
        queryCache.Verify(item => item.Store(
            It.IsAny<string>(),
            It.IsAny<object>(),
            It.IsAny<ProjectTargetFrameworkCacheEntry>(),
            It.IsAny<long>()), Times.Never);
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
