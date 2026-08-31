namespace Roslyn.Workbench.Mcp.Workspace.Paths;

/// <summary>
/// Creates path-projection services bound to a workspace root.
/// </summary>
internal interface IWorkspacePathServiceFactory
{
    /// <summary>
    /// Creates a service that projects canonical paths for the selected workspace.
    /// </summary>
    /// <param name="workspaceIdentity">The identity of the workspace being processed.</param>
    /// <returns>A path service bound to the workspace root.</returns>
    IWorkspacePathService Create(WorkspaceIdentity? workspaceIdentity);
}
