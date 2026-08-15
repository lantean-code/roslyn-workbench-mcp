namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal interface IWorkspaceProjectInputResolver
{
    WorkspaceProjectInputResolution Resolve(
        string? projectPath,
        WorkspaceMsBuildProperties? msBuildProperties = null);
}
