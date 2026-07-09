namespace Roslyn.Workbench.Mcp.Workspace.Loading;

internal sealed class WorkspaceHostServicesAccessor
{
    public WorkspaceHostServicesAccessor(ICodeActionRuntime codeActionRuntime)
    {
        WorkspaceHostServices = codeActionRuntime.WorkspaceHostServices;
    }

    public HostServices? WorkspaceHostServices { get; }
}
