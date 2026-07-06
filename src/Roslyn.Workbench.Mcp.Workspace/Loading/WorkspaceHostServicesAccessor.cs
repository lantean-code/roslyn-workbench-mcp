namespace Roslyn.Workbench.Mcp.Workspace.Loading;

internal sealed class WorkspaceHostServicesAccessor
{
    public WorkspaceHostServicesAccessor(HostServices? workspaceHostServices)
    {
        WorkspaceHostServices = workspaceHostServices;
    }

    public HostServices? WorkspaceHostServices { get; }
}
