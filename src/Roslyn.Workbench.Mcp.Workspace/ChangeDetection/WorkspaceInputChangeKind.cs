namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal enum WorkspaceInputChangeKind
{
    Changed,
    Created,
    Deleted,
    Renamed,
    WatcherError,
    MetadataChanged,
    ManifestIncomplete,
}
