namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

/// <summary>
/// Identifies the mechanism that discovered a Workspace input change.
/// </summary>
internal enum WorkspaceInputChangeDetectionSource
{
    /// <summary>
    /// The operating-system file watcher reported the change.
    /// </summary>
    FileSystemWatcher,

    /// <summary>
    /// A comparison of stored and current filesystem metadata reported the change.
    /// </summary>
    MetadataPolling,

    /// <summary>
    /// Manifest construction or validation could not certify a complete stable input set.
    /// </summary>
    ManifestValidation,
}
