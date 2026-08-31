using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.Workspace.Projects.Caching;

/// <summary>
/// Retains the target frameworks resolved for one project and their cache charge.
/// </summary>
internal sealed class ProjectTargetFrameworkCacheEntry
{
    /// <summary>
    /// Gets the target frameworks resolved for the project.
    /// </summary>
    public ImmutableArray<string> TargetFrameworks { get; }

    /// <summary>
    /// Gets the estimated cache charge for the retained framework names.
    /// </summary>
    public long Size { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectTargetFrameworkCacheEntry"/> class.
    /// </summary>
    /// <param name="targetFrameworks">The target frameworks discovered for the cached project.</param>
    public ProjectTargetFrameworkCacheEntry(IReadOnlyList<string> targetFrameworks)
    {
        TargetFrameworks = ImmutableArray.CreateRange(targetFrameworks);
        Size = TargetFrameworks.Length + 1;
    }
}
