namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal interface IWorkspaceInputChangeMonitor : IDisposable
{
    WorkspaceInputChange? Change { get; }

    void Track(WorkspaceInputManifest manifest);

    void WaitForPendingEvents(CancellationToken cancellationToken);
}
