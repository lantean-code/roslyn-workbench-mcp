namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection.Caching;

internal sealed class ProjectTargetFrameworkCacheEntry
{
    public ProjectTargetFrameworksResult Result { get; }

    public long Size { get; }

    public ProjectTargetFrameworkCacheEntry(ProjectTargetFrameworksResult result)
    {
        var targetFrameworks = result.TargetFrameworks.ToArray();

        Result = ProjectTargetFrameworksResult.Succeeded(targetFrameworks);
        Size = targetFrameworks.Length + 1;
    }
}
