using Roslyn.Workbench.Mcp.Workspace.Caching;

namespace Roslyn.Workbench.Mcp.Workspace.Projects.Caching;

internal sealed record ProjectTargetFrameworkCacheKey : IWorkspaceQueryCacheKey
{
    public string ProjectPath { get; }

    public ProjectTargetFrameworkCacheKey(string projectPath)
    {
        ProjectPath = projectPath;
    }
}
