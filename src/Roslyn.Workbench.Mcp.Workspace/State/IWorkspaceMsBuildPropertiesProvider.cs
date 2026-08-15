namespace Roslyn.Workbench.Mcp.Workspace.State;

internal interface IWorkspaceMsBuildPropertiesProvider
{
    WorkspaceMsBuildProperties? Get(Guid workspaceId);
}
