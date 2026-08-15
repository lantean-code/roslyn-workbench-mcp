namespace Roslyn.Workbench.Mcp.Workspace.Loading;

internal interface IWorkspaceMsBuildPropertiesResolver
{
    WorkspaceMsBuildPropertiesResolution Resolve(WorkspaceMsBuildProperties? properties);
}
