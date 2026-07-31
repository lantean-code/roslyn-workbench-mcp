using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.Workspace.Projects.Caching;

internal sealed class ProjectTargetFrameworkCacheEntry
{
    public ImmutableArray<string> TargetFrameworks { get; }

    public long Size { get; }

    public ProjectTargetFrameworkCacheEntry(IReadOnlyList<string> targetFrameworks)
    {
        TargetFrameworks = ImmutableArray.CreateRange(targetFrameworks);
        Size = TargetFrameworks.Length + 1;
    }
}
