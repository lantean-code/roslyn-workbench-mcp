namespace Roslyn.Workbench.Mcp.Workspace.Loading;

/// <summary>
/// Resolves the workspace root and verifies physical path containment within that root.
/// </summary>
internal interface IWorkspaceRootResolver
{
    /// <summary>
    /// Resolves the effective workspace root for a loaded solution or project.
    /// </summary>
    /// <param name="loadedPath">The solution or project path being loaded.</param>
    /// <param name="requestedRoot">The optional root requested by the caller.</param>
    /// <returns>The canonical workspace root, or <see langword="null"/> when the inputs are invalid or incompatible.</returns>
    string? Resolve(string loadedPath, string? requestedRoot);

    /// <summary>
    /// Determines whether a physical path is contained by a workspace root.
    /// </summary>
    /// <param name="workspaceRoot">The canonical workspace root.</param>
    /// <param name="path">The path to test.</param>
    /// <returns><see langword="true"/> when the path is physically contained by the root; otherwise, <see langword="false"/>.</returns>
    bool Contains(string workspaceRoot, string path);
}
