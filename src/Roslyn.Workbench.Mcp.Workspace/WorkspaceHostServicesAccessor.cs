namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed class WorkspaceHostServicesAccessor
{
    public WorkspaceHostServicesAccessor(HostServices? workspaceHostServices)
    {
        WorkspaceHostServices = workspaceHostServices;
    }

    public HostServices? WorkspaceHostServices { get; }
}
