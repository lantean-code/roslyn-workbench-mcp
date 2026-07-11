namespace Roslyn.Workbench.Mcp.Workspace.Loading;

internal interface IWorkspaceRootResolver
{
    string? Resolve(string loadedPath, string? requestedRoot);

    bool Contains(string workspaceRoot, string path);
}
