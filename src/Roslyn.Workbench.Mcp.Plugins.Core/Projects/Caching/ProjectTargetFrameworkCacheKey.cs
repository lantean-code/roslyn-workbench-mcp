using System.Runtime.CompilerServices;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Projects.Caching;

internal sealed class ProjectTargetFrameworkCacheKey : IEquatable<ProjectTargetFrameworkCacheKey>
{
    private readonly string _projectPath;
    private readonly Solution _solution;

    public ProjectTargetFrameworkCacheKey(Solution solution, string projectPath)
    {
        _solution = solution;
        _projectPath = projectPath;
    }

    public bool Equals(ProjectTargetFrameworkCacheKey? other)
    {
        return other is not null
            && ReferenceEquals(_solution, other._solution)
            && string.Equals(_projectPath, other._projectPath, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as ProjectTargetFrameworkCacheKey);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(RuntimeHelpers.GetHashCode(_solution), StringComparer.Ordinal.GetHashCode(_projectPath));
    }
}
