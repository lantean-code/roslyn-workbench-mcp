namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal interface IWorkspaceExternalInputChangeMonitorFactory
{
    IWorkspaceExternalInputChangeMonitor Create(IReadOnlyList<WorkspaceExternalInputMembership> memberships);
}
