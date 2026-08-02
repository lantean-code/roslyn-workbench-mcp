namespace Roslyn.Workbench.Mcp.Workspace.Paths;

internal interface IWorkspacePathServiceFactory
{
    IWorkspacePathService Create(WorkspaceIdentity? workspaceIdentity);
}
