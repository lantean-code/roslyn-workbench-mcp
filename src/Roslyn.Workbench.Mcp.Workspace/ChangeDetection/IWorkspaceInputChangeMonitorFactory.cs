namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal interface IWorkspaceInputChangeMonitorFactory
{
    IWorkspaceInputChangeMonitor Create(string workspaceRoot);
}
