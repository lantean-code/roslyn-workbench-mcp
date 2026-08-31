using Roslyn.Workbench.Mcp.Workspace.Caching;

namespace Roslyn.Workbench.Mcp.Workspace.Projects.Caching;

/// <summary>
/// Identifies target-framework information cached for a normalized project path.
/// </summary>
internal sealed record ProjectTargetFrameworkCacheKey : IWorkspaceQueryCacheKey
{
    /// <summary>
    /// Gets the normalized project path represented by the key.
    /// </summary>
    public string ProjectPath { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectTargetFrameworkCacheKey"/> class.
    /// </summary>
    /// <param name="projectPath">The normalized project path represented by the cache key.</param>
    public ProjectTargetFrameworkCacheKey(string projectPath)
    {
        ProjectPath = projectPath;
    }
}
