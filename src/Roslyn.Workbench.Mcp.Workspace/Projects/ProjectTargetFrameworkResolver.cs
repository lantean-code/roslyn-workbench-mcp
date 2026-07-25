using Roslyn.Workbench.Mcp.Workspace.Caching;
using Roslyn.Workbench.Mcp.Workspace.Projects.Caching;

namespace Roslyn.Workbench.Mcp.Workspace.Projects;

internal sealed class ProjectTargetFrameworkResolver : IProjectTargetFrameworkResolver
{
    private readonly IProjectStructureService _projectStructureService;
    private readonly IQueryCache _queryCache;

    public ProjectTargetFrameworkResolver(
        IProjectStructureService projectStructureService,
        IQueryCache queryCache)
    {
        _projectStructureService = projectStructureService;
        _queryCache = queryCache;
    }

    public ProjectTargetFrameworksResult Resolve(
        string workspaceId,
        Project project,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var projectPath = project.FilePath;
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return ProjectTargetFrameworksResult.Succeeded();
        }

        var cacheKey = new ProjectTargetFrameworkCacheKey(project.Solution, projectPath);
        if (_queryCache.TryGet<ProjectTargetFrameworkCacheEntry>(workspaceId, cacheKey, out var cacheEntry))
        {
            return cacheEntry.Result;
        }

        var result = _projectStructureService.GetTargetFrameworks(project);
        cancellationToken.ThrowIfCancellationRequested();

        if (result.IsSucceeded)
        {
            Store(workspaceId, cacheKey, result);
        }

        return result;
    }

    public IReadOnlyList<ProjectTargetFrameworksResult> Resolve(
        string workspaceId,
        IReadOnlyList<Project> projects,
        CancellationToken cancellationToken)
    {
        var results = new ProjectTargetFrameworksResult[projects.Count];
        var cacheMisses = new List<(int ResultIndex, Project Project, ProjectTargetFrameworkCacheKey CacheKey)>();

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

            var cacheKey = new ProjectTargetFrameworkCacheKey(project.Solution, projectPath);
            if (_queryCache.TryGet<ProjectTargetFrameworkCacheEntry>(workspaceId, cacheKey, out var cacheEntry))
            {
                results[index] = cacheEntry.Result;
                continue;
            }

            cacheMisses.Add((index, project, cacheKey));
        }

        if (cacheMisses.Count == 0)
        {
            return results;
        }

        var projectsToEvaluate = new Project[cacheMisses.Count];
        for (var index = 0; index < cacheMisses.Count; index++)
        {
            projectsToEvaluate[index] = cacheMisses[index].Project;
        }

        var evaluatedResults = _projectStructureService.GetTargetFrameworks(projectsToEvaluate);
        cancellationToken.ThrowIfCancellationRequested();

        var allEvaluationsSucceeded = true;
        for (var index = 0; index < cacheMisses.Count; index++)
        {
            var (resultIndex, _, _) = cacheMisses[index];
            var result = evaluatedResults[index];

            results[resultIndex] = result;
            allEvaluationsSucceeded &= result.IsSucceeded;
        }

        if (!allEvaluationsSucceeded)
        {
            return results;
        }

        for (var index = 0; index < cacheMisses.Count; index++)
        {
            var (resultIndex, _, cacheKey) = cacheMisses[index];

            Store(workspaceId, cacheKey, results[resultIndex]);
        }

        return results;
    }

    private void Store(
        string workspaceId,
        ProjectTargetFrameworkCacheKey cacheKey,
        ProjectTargetFrameworksResult result)
    {
        var cacheEntry = new ProjectTargetFrameworkCacheEntry(result);

        _queryCache.Store(workspaceId, cacheKey, cacheEntry, cacheEntry.Size);
    }
}
