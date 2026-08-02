namespace Roslyn.Workbench.Mcp.Workspace.Paths;

internal sealed class WorkspacePathServiceFactory : IWorkspacePathServiceFactory
{
    private readonly IWorkspacePathNormalizer _pathNormalizer;

    public WorkspacePathServiceFactory(IWorkspacePathNormalizer pathNormalizer)
    {
        _pathNormalizer = pathNormalizer;
    }

    public IWorkspacePathService Create(WorkspaceIdentity? workspaceIdentity)
    {
        var workspaceRoot = workspaceIdentity?.WorkspaceRoot ?? string.Empty;
        return new WorkspacePathService(workspaceRoot, _pathNormalizer);
    }
}
