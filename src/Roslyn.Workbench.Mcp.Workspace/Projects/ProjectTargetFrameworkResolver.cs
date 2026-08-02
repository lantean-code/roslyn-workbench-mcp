using Roslyn.Workbench.Mcp.Workspace.Caching;
using Roslyn.Workbench.Mcp.Workspace.Projects.Caching;

namespace Roslyn.Workbench.Mcp.Workspace.Projects;

internal sealed class ProjectTargetFrameworkResolver : IProjectTargetFrameworkResolver
{
    private const string _cacheComponentIdentity = "project-target-framework";

    private readonly IProjectStructureService _projectStructureService;
    private readonly IWorkspaceQueryCacheScopeFactory _queryCacheScopeFactory;

    public ProjectTargetFrameworkResolver(
        IProjectStructureService projectStructureService,
        IWorkspaceQueryCacheScopeFactory queryCacheScopeFactory)
    {
        _projectStructureService = projectStructureService;
        _queryCacheScopeFactory = queryCacheScopeFactory;
    }

    public ProjectTargetFrameworksResult Resolve(
        Guid workspaceId,
        Project project,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var projectPath = project.FilePath;
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return ProjectTargetFrameworksResult.Succeeded();
        }

        var cacheScope = _queryCacheScopeFactory.CreateScope(
            workspaceId,
            project.Solution,
            _cacheComponentIdentity);

        var cacheKey = new ProjectTargetFrameworkCacheKey(projectPath);
        var result = cacheScope.GetOrCreateProjected(
            cacheKey,
            factoryCancellationToken =>
            {
                var evaluatedResult = _projectStructureService.GetTargetFrameworks(project);
                factoryCancellationToken.ThrowIfCancellationRequested();
                return evaluatedResult;
            },
            static evaluatedResult => SelectCacheEntry(evaluatedResult),
            static cacheEntry => CreateSuccessfulResult(cacheEntry),
            static value => value.Size,
            cancellationToken);

        return result;
    }

    public IReadOnlyList<ProjectTargetFrameworksResult> Resolve(
        Guid workspaceId,
        IReadOnlyList<Project> projects,
        CancellationToken cancellationToken)
    {
        var results = new ProjectTargetFrameworksResult[projects.Count];
        var cacheMisses = new List<(
            int ResultIndex,
            Project Project,
            IWorkspaceQueryCacheScope CacheScope,
            ProjectTargetFrameworkCacheKey CacheKey)>();

        for (var index = 0; index < projects.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var project = projects[index];
            var projectPath = project.FilePath;
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                results[index] = ProjectTargetFrameworksResult.Succeeded();
                continue;
            }

            var cacheScope = _queryCacheScopeFactory.CreateScope(
                workspaceId,
                project.Solution,
                _cacheComponentIdentity);

            var cacheKey = new ProjectTargetFrameworkCacheKey(projectPath);
            if (cacheScope.TryGet<ProjectTargetFrameworkCacheKey, ProjectTargetFrameworkCacheEntry>(
                cacheKey,
                out var cacheEntry)
                && cacheEntry is not null)
            {
                results[index] = CreateSuccessfulResult(cacheEntry);
                continue;
            }

            cacheMisses.Add((index, project, cacheScope, cacheKey));
        }

        if (cacheMisses.Count == 0)
        {
            return results;
        }

        var projectsToEvaluate = cacheMisses
            .Select(static miss => miss.Project)
            .ToArray();

        var evaluatedResults = _projectStructureService.GetTargetFrameworks(
            projectsToEvaluate,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        for (var index = 0; index < cacheMisses.Count; index++)
        {
            var (resultIndex, _, cacheScope, cacheKey) = cacheMisses[index];
            var result = evaluatedResults[index];
            results[resultIndex] = result;
            if (!result.IsSucceeded)
            {
                continue;
            }

            var cacheEntry = new ProjectTargetFrameworkCacheEntry(result.TargetFrameworks);
            cacheScope.Store(
                cacheKey,
                cacheEntry,
                static value => value.Size);
        }

        return results;
    }

    private static ProjectTargetFrameworkCacheEntry? SelectCacheEntry(
        ProjectTargetFrameworksResult result)
    {
        if (!result.IsSucceeded)
        {
            return null;
        }

        var cacheEntry = new ProjectTargetFrameworkCacheEntry(result.TargetFrameworks);
        return cacheEntry;
    }

    private static ProjectTargetFrameworksResult CreateSuccessfulResult(
        ProjectTargetFrameworkCacheEntry cacheEntry)
    {
        var result = ProjectTargetFrameworksResult.Succeeded(cacheEntry.TargetFrameworks);
        return result;
    }
}
