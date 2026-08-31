namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

/// <summary>
/// Classifies changes and monitoring failures that can invalidate a Workspace snapshot.
/// </summary>
internal enum WorkspaceInputChangeKind
{
    /// <summary>
    /// An existing input's contents or metadata changed.
    /// </summary>
    Changed,

    /// <summary>
    /// A new relevant input was created.
    /// </summary>
    Created,

    /// <summary>
    /// A relevant input was deleted.
    /// </summary>
    Deleted,

    /// <summary>
    /// A relevant input was renamed or moved.
    /// </summary>
    Renamed,

    /// <summary>
    /// The file watcher can no longer provide reliable change coverage.
    /// </summary>
    WatcherError,

    /// <summary>
    /// Polled file or directory metadata differs from the certified manifest.
    /// </summary>
    MetadataChanged,

    /// <summary>
    /// One or more project inputs could not be evaluated for the manifest.
    /// </summary>
    ManifestIncomplete,

    /// <summary>
    /// External glob membership could not be evaluated reliably.
    /// </summary>
    MembershipError,
}
