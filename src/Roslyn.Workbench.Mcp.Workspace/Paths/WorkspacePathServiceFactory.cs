namespace Roslyn.Workbench.Mcp.Workspace.Paths;

/// <summary>
/// Creates path-projection services bound to a workspace root.
/// </summary>
internal sealed class WorkspacePathServiceFactory : IWorkspacePathServiceFactory
{
    private readonly IWorkspacePathNormalizer _pathNormalizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspacePathServiceFactory"/> class.
    /// </summary>
    /// <param name="pathNormalizer">The service used to normalize workspace paths.</param>
    public WorkspacePathServiceFactory(IWorkspacePathNormalizer pathNormalizer)
    {
        _pathNormalizer = pathNormalizer;
    }

    /// <summary>
    /// Creates a service that projects canonical paths for the selected workspace.
    /// </summary>
    /// <param name="workspaceIdentity">The identity of the workspace being processed.</param>
    /// <returns>A path service bound to the workspace root.</returns>
    public IWorkspacePathService Create(WorkspaceIdentity? workspaceIdentity)
    {
        var workspaceRoot = workspaceIdentity?.WorkspaceRoot ?? string.Empty;
        return new WorkspacePathService(workspaceRoot, _pathNormalizer);
    }
}
