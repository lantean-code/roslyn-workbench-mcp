namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal enum WorkspaceInputWatcherEventKind
{
    Changed,
    Created,
    Deleted,
    Renamed,
    Error,
}
