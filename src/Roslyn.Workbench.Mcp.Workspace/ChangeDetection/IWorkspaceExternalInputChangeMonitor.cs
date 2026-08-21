namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal interface IWorkspaceExternalInputChangeMonitor : IDisposable
{
    WorkspaceInputChange? Change { get; }

    void Start();

    void WaitForPendingEvents(CancellationToken cancellationToken);
}
